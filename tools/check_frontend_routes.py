#!/usr/bin/env python3
"""Read-only route and narrow-viewport check on a disposable Release app."""

import argparse
import asyncio
import json
import pathlib
import shutil
import subprocess
import tempfile

from benchmark_frontend_routes import DevTools, free_port, read, ready, wait_for_static_network_idle

ROOT = pathlib.Path(__file__).resolve().parents[1]
ROUTES = (
    "/", "/tv", "/music", "/activity/queue", "/activity/history",
    "/activity/blocklist", "/activity/hygiene", "/settings",
    "/settings/mediamanagement", "/settings/profiles", "/settings/quality",
    "/settings/customformats", "/settings/indexers",
    "/settings/downloadclients", "/settings/importlists", "/settings/connect",
    "/settings/metadata", "/settings/tags", "/settings/general",
    "/settings/ui", "/system", "/system/status",
)


async def check_routes(chrome_url, app_url, width):
    tab = json.loads(read(f"{chrome_url}/json/new?about:blank", "PUT"))
    results = []
    try:
        async with DevTools(tab["webSocketDebuggerUrl"]) as devtools:
            for method in ("Network.enable", "Page.enable", "Runtime.enable"):
                await devtools.command(method)
            await devtools.command("Emulation.setDeviceMetricsOverride", {
                "width": width, "height": 900, "deviceScaleFactor": 1, "mobile": False,
            })
            for route in ROUTES:
                devtools.events.clear()
                await devtools.command("Page.navigate", {"url": app_url + route})
                await ready(devtools, f"location.pathname === {json.dumps(route)} && document.readyState === 'complete'")
                await wait_for_static_network_idle(devtools, app_url)
                await asyncio.sleep(0.3)
                page = await devtools.evaluate("""({
                  title: document.title,
                  text: document.body.innerText.slice(0, 150),
                  bodyWidth: document.body.scrollWidth,
                  viewportWidth: document.documentElement.clientWidth,
                  errorPage: document.body.innerText.includes('Error Loading Page'),
                  loading: document.body.innerText.includes('Loading page...')
                })""")
                exceptions = [event for event in devtools.events
                              if event.get("method") == "Runtime.exceptionThrown"]
                results.append({"route": route, **page, "exceptions": len(exceptions),
                                "overflow": page["bodyWidth"] > page["viewportWidth"] + 1})
    finally:
        read(f"{chrome_url}/json/close/{tab['id']}")
    return results


async def main(args):
    with tempfile.TemporaryDirectory(prefix="plattarr-route-check-") as scratch:
        folder = pathlib.Path(scratch)
        data = folder / "data"
        data.mkdir()
        app_port, chrome_port = free_port(), free_port()
        app_url = f"http://127.0.0.1:{app_port}"
        chrome_url = f"http://127.0.0.1:{chrome_port}"
        (data / "config.xml").write_text(
            "<Config><BindAddress>127.0.0.1</BindAddress>"
            f"<Port>{app_port}</Port><ApiKey>00000000000000000000000000000000</ApiKey>"
            "<AuthenticationMethod>External</AuthenticationMethod></Config>"
        )
        with (folder / "app.log").open("wb") as app_log:
            app = subprocess.Popen([args.dotnet, str(ROOT / "_output/net10.0/Radarr.dll"),
                                    "-nobrowser", f"-data={data}"],
                                   cwd=ROOT, stdout=app_log, stderr=subprocess.STDOUT)
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
                results = []
                for width in (1440, 480):
                    results.extend({"width": width, **result}
                                   for result in await check_routes(chrome_url, app_url, width))
                print(json.dumps(results, indent=2))
                if any(result["errorPage"] or result["loading"] or result["exceptions"] or
                       result["overflow"] or not result["text"] for result in results):
                    raise SystemExit(1)
            finally:
                if chrome is not None:
                    chrome.terminate()
                    chrome.wait(timeout=10)
                app.terminate()
                app.wait(timeout=15)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--chrome", default=shutil.which("google-chrome") or "google-chrome")
    parser.add_argument("--dotnet", default=shutil.which("dotnet") or str(pathlib.Path.home() / ".dotnet/dotnet"))
    asyncio.run(main(parser.parse_args()))
