import importlib.util
import json
from pathlib import Path
import unittest
import urllib.request


spec = importlib.util.spec_from_file_location("registry_metadata", Path(__file__).resolve().parents[1] / "registry_metadata.py")
metadata = importlib.util.module_from_spec(spec)
spec.loader.exec_module(metadata)


class Registry:
    repository = "example/plattarr"

    def __init__(self):
        self.documents = {}
        self.records = []
        self.tags = {}
        self.mutations = []
        self.blobs = {}
        self.amd64 = self.add({"schemaVersion": 2, "mediaType": "application/vnd.oci.image.manifest.v1+json", "config": {"digest": "sha256:" + "1" * 64}, "layers": [{"mediaType": "application/vnd.oci.image.layer.v1.tar+gzip", "digest": "sha256:" + "2" * 64}]})
        self.arm64 = self.add({"schemaVersion": 2, "mediaType": "application/vnd.oci.image.manifest.v1+json", "config": {"digest": "sha256:" + "3" * 64}, "layers": [{"mediaType": "application/vnd.oci.image.layer.v1.tar+gzip", "digest": "sha256:" + "4" * 64}]})
        self.unsafe = self.attestation({"predicate": {"buildDefinition": {"internalParameters": {"github_event_payload": {"pusher": {"email": "private@example.invalid"}}}}}})
        self.safe = self.attestation({"predicate": {"buildDefinition": {"internalParameters": {}}, "runDetails": {"builder": {"id": "example-builder"}}}})
        self.index = self.add({"schemaVersion": 2, "mediaType": "application/vnd.oci.image.index.v1+json", "annotations": {"example": "retained"}, "manifests": [
            self.descriptor(self.amd64, "amd64", "linux"),
            self.descriptor(self.arm64, "arm64", "linux"),
            self.descriptor(self.unsafe, "unknown", "unknown"),
            self.descriptor(self.safe, "unknown", "unknown"),
        ]}, ["develop", "sha-old"])

    def add(self, document, tags=None):
        reference = metadata.digest(metadata.encode(document))
        self.documents[reference] = document
        self.records.append({"version_id": len(self.records) + 1, "digest": reference, "tags": sorted(tags or [])})
        for tag in tags or []:
            self.tags[tag] = reference
        return reference

    def attestation(self, document):
        reference = metadata.digest(metadata.encode(document))
        self.blobs[reference] = document
        return self.add({"schemaVersion": 2, "mediaType": "application/vnd.oci.image.manifest.v1+json", "layers": [{"mediaType": "application/vnd.in-toto+json", "digest": reference}]})

    def descriptor(self, reference, arch, system):
        return {"mediaType": "application/vnd.oci.image.manifest.v1+json", "digest": reference, "size": len(metadata.encode(self.documents[reference])), "platform": {"architecture": arch, "os": system}}

    def versions(self):
        return self.records.copy()

    def manifest(self, reference):
        reference = self.tags.get(reference, reference)
        return reference, self.documents[reference]

    def blob(self, reference):
        return self.blobs[reference]

    def put_manifest(self, tag, document):
        reference = metadata.digest(metadata.encode(document))
        self.documents[reference] = document
        self.tags[tag] = reference
        self.mutations.append(("put", tag))

    def delete_version(self, version):
        if version["digest"] in self.tags.values():
            raise AssertionError("Attempted to delete a tagged manifest")
        self.mutations.append(("delete", version["digest"]))
        self.records = [item for item in self.records if item["version_id"] != version["version_id"]]
        del self.documents[version["digest"]]


class RegistryMetadataTests(unittest.TestCase):
    def test_registry_authorization_does_not_follow_a_blob_cdn_redirect(self):
        request = urllib.request.Request("https://ghcr.io/v2/example/plattarr/blobs/example", headers={"Authorization": "Bearer placeholder"})
        redirected = metadata.PrivateRedirectHandler().redirect_request(request, None, 302, "Found", {}, "https://cdn.example.invalid/blob")
        self.assertIsNone(redirected.get_header("Authorization"))

    def test_same_origin_redirect_keeps_registry_authorization(self):
        request = urllib.request.Request("https://ghcr.io/v2/example/plattarr/blobs/example", headers={"Authorization": "Bearer placeholder"})
        redirected = metadata.PrivateRedirectHandler().redirect_request(request, None, 302, "Found", {}, "https://ghcr.io/v2/example/plattarr/blobs/other")
        self.assertEqual(redirected.get_header("Authorization"), "Bearer placeholder")

    def test_metadata_redirect_cannot_downgrade_tls(self):
        request = urllib.request.Request("https://ghcr.io/v2/example/plattarr/blobs/example")
        with self.assertRaisesRegex(ValueError, "without TLS"):
            metadata.PrivateRedirectHandler().redirect_request(request, None, 302, "Found", {}, "http://cdn.example.invalid/blob")

    def test_inventory_does_not_export_private_event_values(self):
        registry = Registry()
        plan = metadata.inventory(registry)
        self.assertEqual([item["digest"] for item in plan["attestations"]], [registry.unsafe])
        self.assertEqual(plan["rewrites"][0]["remove"], [registry.unsafe])
        self.assertNotIn("private@example.invalid", json.dumps(plan))
        self.assertNotIn("github_event_payload", json.dumps(plan))

    def test_cleanup_preserves_runtime_descriptors_safe_provenance_and_tags(self):
        registry = Registry()
        original = registry.documents[registry.index]
        plan = metadata.inventory(registry)
        result = metadata.apply_plan(registry, plan, metadata.plan_hash(plan))
        for tag in ["develop", "sha-old"]:
            _, replacement = registry.manifest(tag)
            self.assertEqual(replacement["manifests"], [entry for entry in original["manifests"] if entry["digest"] != registry.unsafe])
            self.assertEqual(replacement["annotations"], original["annotations"])
        self.assertEqual(result["runtime_manifests_retained"], 2)
        self.assertIn(registry.amd64, registry.documents)
        self.assertIn(registry.arm64, registry.documents)
        self.assertIn(registry.safe, registry.documents)
        self.assertEqual([entry[1] for entry in registry.mutations if entry[0] == "delete"], [registry.index, registry.unsafe])

    def test_changed_plan_cannot_mutate_registry(self):
        registry = Registry()
        with self.assertRaisesRegex(ValueError, "Inventory changed"):
            metadata.apply_plan(registry, metadata.inventory(registry), "0" * 64)
        self.assertEqual(registry.mutations, [])

    def test_moved_tag_is_not_overwritten(self):
        registry = Registry()
        plan = metadata.inventory(registry)
        registry.tags["develop"] = registry.amd64
        with self.assertRaisesRegex(ValueError, "tag moved"):
            metadata.apply_plan(registry, plan, metadata.plan_hash(plan))
        self.assertEqual(registry.mutations, [])

    def test_runtime_manifest_cannot_be_in_delete_plan(self):
        registry = Registry()
        plan = metadata.inventory(registry)
        plan["attestations"] = [registry.records[0]]
        with self.assertRaisesRegex(ValueError, "non-attestation"):
            metadata.apply_plan(registry, plan, metadata.plan_hash(plan))
        self.assertEqual(registry.mutations, [])

    def test_runtime_descriptor_cannot_be_removed(self):
        registry = Registry()
        with self.assertRaisesRegex(ValueError, "runtime image"):
            metadata.sanitized_index(registry.documents[registry.index], [registry.amd64])

    def test_tagged_attestation_requires_separate_review(self):
        registry = Registry()
        next(item for item in registry.records if item["digest"] == registry.unsafe)["tags"] = ["unexpected"]
        with self.assertRaisesRegex(ValueError, "separate review"):
            metadata.inventory(registry)
        self.assertEqual(registry.mutations, [])

    def test_existing_minimal_provenance_requires_no_changes(self):
        registry = Registry()
        registry.blobs = {reference: {"predicate": {"buildDefinition": {"internalParameters": {}}}} for reference in registry.blobs}
        plan = metadata.inventory(registry)
        self.assertEqual(plan["attestations"], [])
        self.assertEqual(plan["rewrites"], [])


if __name__ == "__main__":
    unittest.main()
