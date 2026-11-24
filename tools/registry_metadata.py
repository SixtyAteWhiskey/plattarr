#!/usr/bin/env python3
"""Review and remove legacy GHCR attestations containing full workflow events.

Only metadata manifests are replaced or removed. Runtime image manifests and
layers are retained. Apply requires the digest of a previously reviewed plan.
"""

import argparse
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import re
import time
import urllib.parse
import urllib.request


MANIFEST_TYPES = ", ".join([
    "application/vnd.oci.image.index.v1+json",
    "application/vnd.oci.image.manifest.v1+json",
    "application/vnd.docker.distribution.manifest.list.v2+json",
    "application/vnd.docker.distribution.manifest.v2+json",
])


def encode(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def plan_hash(plan):
    return hashlib.sha256(encode(plan)).hexdigest()


def contains_event(value):
    if isinstance(value, dict):
        return "github_event_payload" in value or any(contains_event(v) for v in value.values())
    if isinstance(value, list):
        return any(contains_event(v) for v in value)
    return False


def is_attestation(manifest):
    layers = manifest.get("layers", [])
    return bool(layers) and all(layer.get("mediaType") == "application/vnd.in-toto+json" for layer in layers)


def sanitized_index(index, remove):
    entries = index.get("manifests", [])
    if not entries or not set(remove).issubset({entry["digest"] for entry in entries}):
        raise ValueError("Plan does not match the image index")
    kept = [entry for entry in entries if entry["digest"] not in remove]
    runtime = [entry for entry in entries if entry.get("platform", {}).get("os") == "linux"]
    if not runtime or any(entry["digest"] in remove for entry in runtime):
        raise ValueError("Refusing to remove a runtime image")
    if any(entry.get("platform", {}).get("architecture") != "unknown" for entry in entries if entry["digest"] in remove):
        raise ValueError("Only attestation descriptors may be removed")
    return {**index, "manifests": kept}


class PrivateRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        destination = urllib.parse.urlsplit(new_url)
        source = urllib.parse.urlsplit(request.full_url)
        if destination.scheme != "https":
            raise ValueError("Refusing a metadata redirect without TLS")
        redirected = super().redirect_request(request, response, code, message, headers, new_url)
        if (source.scheme, source.netloc) != (destination.scheme, destination.netloc):
            redirected.remove_header("Authorization")
        return redirected


class Client:
    def __init__(self, repository, token, actor):
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise ValueError("Invalid repository")
        self.repository = repository.lower()
        owner, package = repository.split("/")
        self.token = token
        owner_data = self.api("GET", f"users/{owner}")
        owner_route = "orgs" if owner_data["type"] == "Organization" else "users"
        self.package_path = f"{owner_route}/{owner}/packages/container/{package}"
        basic = base64.b64encode(f"{actor}:{token}".encode()).decode()
        query = urllib.parse.urlencode({"service": "ghcr.io", "scope": f"repository:{self.repository}:pull,push"})
        data, _ = self.request("GET", "https://ghcr.io/token?" + query, {"Authorization": "Basic " + basic})
        self.registry_token = json.loads(data)["token"]

    @staticmethod
    def request(method, url, headers, body=None):
        request = urllib.request.Request(url, data=body, headers=headers, method=method)
        opener = urllib.request.build_opener(PrivateRedirectHandler())
        with opener.open(request, timeout=30) as response:
            data = response.read(32 * 1024 * 1024 + 1)
            if len(data) > 32 * 1024 * 1024:
                raise ValueError("Metadata response exceeded limit")
            return data, response.headers

    def api(self, method, path):
        data, _ = self.request(method, "https://api.github.com/" + path, {
            "Authorization": "Bearer " + self.token,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
        })
        return json.loads(data) if data else None

    def versions(self):
        versions = {}
        for page in range(1, 1001):
            batch = self.api("GET", f"{self.package_path}/versions?per_page=100&page={page}")
            for version in batch:
                if not re.fullmatch(r"sha256:[0-9a-f]{64}", version["name"]):
                    raise ValueError("Unexpected package version name")
                versions[version["id"]] = {
                    "version_id": version["id"],
                    "digest": version["name"],
                    "tags": sorted(version["metadata"]["container"]["tags"]),
                }
            if len(batch) < 100:
                return list(versions.values())
        raise ValueError("Package inventory exceeded limit")

    def registry(self, method, kind, reference, body=None, media_type=None):
        headers = {"Authorization": "Bearer " + self.registry_token, "Accept": MANIFEST_TYPES}
        if media_type:
            headers["Content-Type"] = media_type
        return self.request(method, f"https://ghcr.io/v2/{self.repository}/{kind}/{reference}", headers, body)

    def manifest(self, reference):
        data, headers = self.registry("GET", "manifests", reference)
        actual = digest(data)
        if headers.get("Docker-Content-Digest", actual) != actual:
            raise ValueError("Registry manifest digest mismatch")
        return actual, json.loads(data)

    def blob(self, reference):
        data, _ = self.registry("GET", "blobs", reference)
        if digest(data) != reference:
            raise ValueError("Registry metadata blob digest mismatch")
        return json.loads(data)

    def put_manifest(self, tag, manifest):
        data = encode(manifest)
        _, headers = self.registry("PUT", "manifests", tag, data, manifest["mediaType"])
        if headers.get("Docker-Content-Digest", digest(data)) != digest(data):
            raise ValueError("Published metadata digest mismatch")

    def delete_version(self, version):
        current = self.api("GET", f"{self.package_path}/versions/{version['version_id']}")
        if current["name"] != version["digest"]:
            raise ValueError("Package version changed")
        for tag in current["metadata"]["container"]["tags"]:
            current_digest, _ = self.manifest(tag)
            if current_digest == version["digest"]:
                raise ValueError("Refusing to delete metadata still referenced by a tag")
        self.api("DELETE", f"{self.package_path}/versions/{version['version_id']}")
        time.sleep(1)


def inventory(client):
    versions = client.versions()

    def inspect(version):
        actual, manifest = client.manifest(version["digest"])
        if actual != version["digest"]:
            raise ValueError("Package inventory digest mismatch")
        unsafe = is_attestation(manifest) and any(contains_event(client.blob(layer["digest"])) for layer in manifest["layers"])
        if unsafe and version["tags"]:
            raise ValueError("Tagged standalone attestation requires separate review")
        return version, manifest, unsafe

    with ThreadPoolExecutor(max_workers=6) as pool:
        inspected = list(pool.map(inspect, versions))
    unsafe = {version["digest"] for version, _, flagged in inspected if flagged}
    rewrites = []
    for version, manifest, _ in inspected:
        remove = sorted(entry["digest"] for entry in manifest.get("manifests", []) if entry["digest"] in unsafe)
        if remove:
            sanitized_index(manifest, remove)
            rewrites.append({**version, "remove": remove})
    return {
        "schema": 1,
        "repository": client.repository,
        "rewrites": sorted(rewrites, key=lambda item: item["digest"]),
        "attestations": sorted((version for version, _, flagged in inspected if flagged), key=lambda item: item["digest"]),
    }


def apply_plan(client, plan, expected_hash):
    if not re.fullmatch(r"[0-9a-f]{64}", expected_hash) or plan_hash(plan) != expected_hash:
        raise ValueError("Inventory changed; review and back up a fresh plan before applying")
    if plan["repository"] != client.repository:
        raise ValueError("Repository does not match the reviewed plan")
    unsafe = {item["digest"] for item in plan["attestations"]}
    for item in plan["attestations"]:
        actual, manifest = client.manifest(item["digest"])
        if actual != item["digest"] or not is_attestation(manifest):
            raise ValueError("Refusing to delete a non-attestation manifest")

    replacements = []
    runtime_digests = set()
    for item in plan["rewrites"]:
        if not set(item["remove"]).issubset(unsafe):
            raise ValueError("Unreviewed attestation in replacement plan")
        actual, original = client.manifest(item["digest"])
        if actual != item["digest"]:
            raise ValueError("Image index changed")
        replacement = sanitized_index(original, item["remove"])
        replacement_digest = digest(encode(replacement))
        for tag in item["tags"]:
            current, _ = client.manifest(tag)
            if current not in {actual, replacement_digest}:
                raise ValueError("A tag moved; refusing to overwrite another publication")
        runtime_digests.update(entry["digest"] for entry in replacement["manifests"] if entry.get("platform", {}).get("os") == "linux")
        replacements.append((item, replacement, replacement_digest))

    rewritten = 0
    for item, replacement, replacement_digest in replacements:
        for tag in item["tags"]:
            current, _ = client.manifest(tag)
            if current not in {item["digest"], replacement_digest}:
                raise ValueError("A tag moved during the cleanup")
            if current != replacement_digest:
                client.put_manifest(tag, replacement)
            observed, _ = client.manifest(tag)
            if observed != replacement_digest:
                raise ValueError("Replacement tag verification failed")
            rewritten += 1
    for item in plan["rewrites"] + plan["attestations"]:
        client.delete_version(item)
    for reference in sorted(runtime_digests):
        actual, _ = client.manifest(reference)
        if actual != reference:
            raise ValueError("Runtime image manifest was not retained")
    return {"rewritten_tags": rewritten, "removed_index_versions": len(plan["rewrites"]), "removed_attestation_versions": len(plan["attestations"]), "runtime_manifests_retained": len(runtime_digests)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["inventory", "apply"])
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--expected-plan-sha256", default="")
    args = parser.parse_args()
    client = Client(os.environ["GITHUB_REPOSITORY"], os.environ["GH_TOKEN"], os.environ["GITHUB_ACTOR"])
    plan = inventory(client)
    result = {"plan_sha256": plan_hash(plan), "plan": plan}
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({"plan_sha256": result["plan_sha256"], "affected_indexes": len(plan["rewrites"]), "affected_attestations": len(plan["attestations"])}))
    if args.mode == "apply":
        applied = apply_plan(client, plan, args.expected_plan_sha256)
        print(json.dumps(applied))
        args.output.with_suffix(".result.json").write_text(json.dumps(applied, indent=2) + "\n")


if __name__ == "__main__":
    main()
