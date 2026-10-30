#!/usr/bin/env python3
"""Measure browser startup and Settings navigation on a disposable Release app.

Requires Chrome and Python's websockets package. Build Release and the desired
production UI first. The script starts an isolated loopback app and removes all
scratch data and browser state when it exits.
"""

import argparse
import asyncio
import json
import pathlib
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.request

import websockets

ROOT = pathlib.Path(__file__).resolve().parents[1]
API_KEY = "00000000000000000000000000000000"
PROFILES = {
    "fast": {"latency": 0, "downloadThroughput": -1, "uploadThroughput": -1},
    "slow": {"latency": 150, "downloadThroughput": 200_000, "uploadThroughput": 200_000},
}


def free_port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def read(url, method="GET"):
    request = urllib.request.Request(url, method=method)
    with urllib.request.urlopen(request, timeout=10) as response:
        return response.read()


class DevTools:
    def __init__(self, socket_url):
        self.socket_url = socket_url
        self.events = []
        self.sequence = 0

    async def __aenter__(self):
        self.connection = await websockets.connect(self.socket_url, max_size=10_000_000)
        return self

    async def __aexit__(self, *_):
        await self.connection.close()

    async def command(self, method, params=None, timeout=30):
        self.sequence += 1
        command_id = self.sequence
        await self.connection.send(json.dumps({
            "id": command_id,
            "method": method,
            "params": params or {},
        }))
        while True:
            message = json.loads(await asyncio.wait_for(self.connection.recv(), timeout))
            if message.get("id") == command_id:
                if "error" in message:
                    raise RuntimeError(message["error"])
                return message.get("result", {})
            self.events.append(message)

    async def evaluate(self, expression, await_promise=False):
        result = await self.command("Runtime.evaluate", {
            "expression": expression,
            "returnByValue": True,
            "awaitPromise": await_promise,
        })
        if "exceptionDetails" in result:
            raise RuntimeError(result["exceptionDetails"])
        return result["result"].get("value")


async def ready(devtools, expression):
    deadline = time.monotonic() + 40
    while time.monotonic() < deadline:
        if await devtools.evaluate(expression):
            return
        await asyncio.sleep(0.05)
    raise TimeoutError(f"Page did not become ready: {expression}")


async def click_and_time(devtools, href, condition):
    expression = f"""new Promise((resolve, reject) => {{
      const link = document.querySelector('a[href="{href}"]');
      if (!link) {{ reject(new Error('Missing link: {href}')); return; }}
      const start = performance.now();
      link.click();
      const deadline = start + 20000;
      const timer = setInterval(() => {{
        if ({condition}) {{ clearInterval(timer); resolve(performance.now() - start); }}
        else if (performance.now() > deadline) {{
          clearInterval(timer); reject(new Error('Route timed out: {href}'));
        }}
      }}, 20);
    }})"""
    return round(await devtools.evaluate(expression, await_promise=True), 1)


async def script_metrics(devtools):
    result = await devtools.command("Performance.getMetrics")
    values = {item["name"]: item["value"] for item in result["metrics"]}
    return {
        name: round(values[name] * 1000, 1)
        for name in ("ScriptDuration", "V8CompileDuration")
    }


async def wait_for_static_network_idle(devtools, base_url):
    active = {}

    def collect(event):
        method = event.get("method")
        params = event.get("params", {})
        request_id = params.get("requestId")
        if method == "Network.requestWillBeSent":
            url = params["request"]["url"]
            # A worker has an empty loaderId and its loadingFinished event is
            # reported in a different DevTools target, not this page target.
            if url.startswith(base_url) and "/api/" not in url and params.get("loaderId"):
                active[request_id] = url
                return True
        elif method in ("Network.loadingFinished", "Network.loadingFailed"):
            if request_id in active:
                del active[request_id]
                return True
        return False

    for event in devtools.events:
        collect(event)
    deadline = time.monotonic() + 20
    idle_since = time.monotonic()
    while time.monotonic() < deadline:
        if not active and time.monotonic() - idle_since >= 0.5:
            return
        try:
            event = json.loads(await asyncio.wait_for(devtools.connection.recv(), 0.1))
        except asyncio.TimeoutError:
            continue
        devtools.events.append(event)
        if collect(event):
            idle_since = time.monotonic()
    raise TimeoutError(f"Static resources did not settle: {list(active.values())}")


def transfer_bytes(events, base_url):
    responses = {}
    sizes = {}
    exceptions = 0
    for event in events:
        method = event.get("method")
        params = event.get("params", {})
        if method == "Network.responseReceived":
            responses[params["requestId"]] = params["response"]["url"]
        elif method == "Network.loadingFinished":
            sizes[params["requestId"]] = params["encodedDataLength"]
        elif method == "Runtime.exceptionThrown":
            exceptions += 1
    total = sum(
        sizes[request_id]
        for request_id, url in responses.items()
        if request_id in sizes and url.startswith(base_url) and "/api/" not in url
    )
    return total, exceptions


async def sample(chrome_url, app_url, profile):
    tab = json.loads(read(f"{chrome_url}/json/new?about:blank", "PUT"))
    try:
        async with DevTools(tab["webSocketDebuggerUrl"]) as devtools:
            for method in ("Network.enable", "Page.enable", "Runtime.enable", "Performance.enable"):
                await devtools.command(method)
            await devtools.command("Network.setCacheDisabled", {"cacheDisabled": True})
            await devtools.command("Network.emulateNetworkConditions", {
                "offline": False,
                **PROFILES[profile],
            })
            await devtools.command("Page.navigate", {"url": app_url + "/"})
            await ready(devtools, "location.pathname === '/' && document.body && document.body.innerText.includes('Add New')")
            await asyncio.sleep(0.2)
            first_render_ms = round(await devtools.evaluate("performance.now()"), 1)
            cold_metrics = await script_metrics(devtools)
            await wait_for_static_network_idle(devtools, app_url)
            cold_bytes, cold_exceptions = transfer_bytes(devtools.events, app_url)

            await devtools.command("Network.setCacheDisabled", {"cacheDisabled": False})
            settings_condition = "location.pathname === '/settings' && document.title.startsWith('Settings -')"
            movies_condition = "location.pathname === '/' && document.title === 'Radarr' && document.body.innerText.includes('Add New')"
            first_settings_ms = await click_and_time(devtools, "/settings", settings_condition)
            settings_metrics = await script_metrics(devtools)
            await click_and_time(devtools, "/", movies_condition)
            repeat_settings_ms = await click_and_time(devtools, "/settings", settings_condition)
            _, all_exceptions = transfer_bytes(devtools.events, app_url)
            return {
                "first_render_ms": first_render_ms,
                "script_ms": cold_metrics["ScriptDuration"],
                "compile_ms": cold_metrics["V8CompileDuration"],
                "first_settings_script_ms": round(settings_metrics["ScriptDuration"] - cold_metrics["ScriptDuration"], 1),
                "first_settings_compile_ms": round(settings_metrics["V8CompileDuration"] - cold_metrics["V8CompileDuration"], 1),
                "initial_transfer_bytes": cold_bytes,
                "first_settings_ms": first_settings_ms,
                "repeat_settings_ms": repeat_settings_ms,
                "runtime_exceptions": all_exceptions,
                "cold_runtime_exceptions": cold_exceptions,
            }
    finally:
        read(f"{chrome_url}/json/close/{tab['id']}")


async def benchmark(args):
    with tempfile.TemporaryDirectory(prefix="plattarr-frontend-benchmark-") as scratch:
        folder = pathlib.Path(scratch)
        data = folder / "data"
        data.mkdir()
        app_port = free_port()
        chrome_port = free_port()
        app_url = f"http://127.0.0.1:{app_port}"
        chrome_url = f"http://127.0.0.1:{chrome_port}"
        (data / "config.xml").write_text(
            "<Config><BindAddress>127.0.0.1</BindAddress>"
            f"<Port>{app_port}</Port><ApiKey>{API_KEY}</ApiKey>"
            "<AuthenticationMethod>External</AuthenticationMethod></Config>"
        )
        app_log = (folder / "app.log").open("wb")
        app = subprocess.Popen(
            [args.dotnet, str(ROOT / "_output/net10.0/Radarr.dll"),
             "-nobrowser", f"-data={data}"],
            cwd=ROOT, stdout=app_log, stderr=subprocess.STDOUT,
        )
        chrome = None
        try:
            for _ in range(1200):
                if app.poll() is not None:
                    raise RuntimeError(f"Scratch app exited with {app.returncode}")
                try:
                    read(app_url + "/ping")
                    break
                except Exception:
                    await asyncio.sleep(0.1)
            else:
                raise TimeoutError("Scratch app did not start")

            chrome = subprocess.Popen([
                args.chrome, "--headless=new", "--no-sandbox", "--disable-gpu",
                "--disable-background-networking", "--remote-allow-origins=*",
                f"--remote-debugging-port={chrome_port}",
                f"--user-data-dir={folder / 'chrome'}", "about:blank",
            ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            for _ in range(100):
                if chrome.poll() is not None:
                    raise RuntimeError("Chrome exited before DevTools was ready")
                try:
                    read(chrome_url + "/json/version")
                    break
                except Exception:
                    await asyncio.sleep(0.1)
            else:
                raise TimeoutError("Chrome DevTools did not start")

            results = [await sample(chrome_url, app_url, args.profile) for _ in range(args.samples)]
            print(json.dumps({
                "profile": args.profile,
                "samples": results,
                "fixture": "empty disposable Release instance",
                "chrome": args.chrome,
            }, indent=2))
        finally:
            if chrome is not None:
                chrome.terminate()
                chrome.wait(timeout=10)
            app.terminate()
            app.wait(timeout=15)
            app_log.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILES, default="fast")
    parser.add_argument("--samples", type=int, default=5)
    parser.add_argument("--chrome", default=shutil.which("google-chrome") or "google-chrome")
    parser.add_argument("--dotnet", default=shutil.which("dotnet") or str(pathlib.Path.home() / ".dotnet/dotnet"))
    args = parser.parse_args()
    if args.samples < 1:
        parser.error("--samples must be positive")
    asyncio.run(benchmark(args))


if __name__ == "__main__":
    main()
