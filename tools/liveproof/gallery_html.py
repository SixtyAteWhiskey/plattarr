#!/usr/bin/env python3
"""Turn a gallery run (shots/*.jpg + manifest.json from gallery.py) into one self-contained HTML page.

    [GALLERY_NOTE='text shown under the title'] python3 tools/liveproof/gallery_html.py <gallery dir> [output.html]

The images are embedded as data: URIs, so the file opens anywhere and can be published as it is. The page holds no host
name, address or key: the manifest carries only paths and counts. Python 3 standard library only.
"""
import base64
import html
import json
import os
import sys

PAGE = """<title>Plattarr UI Gallery</title>
<meta name="claude:viewport-fit-sides" content="cover">
<style>
/* Layout: a summary strip, a section filter, then a grid of screenshots; a click opens the full-size shot. */
:root {
  --bg: #f4f6f8;
  --surface: #ffffff;
  --fg: #16202a;
  --muted: #5b6875;
  --line: #d9e0e6;
  --accent: #0b6e8f;
  --good: #1b7f4c;
  --warn: #9a6200;
  --bad: #b42318;
  --bad-bg: #fdecea;
  --warn-bg: #fff4dc;
  --good-bg: #e6f4ec;
  --mono: ui-monospace, "SF Mono", "Cascadia Mono", Menlo, Consolas, monospace;
  --sans: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --bg: #0f161c; --surface: #17212a; --fg: #e5ebf0; --muted: #94a3b0; --line: #2a3945; --accent: #52b6d6;
    --good: #5ccf94; --warn: #f0b95a; --bad: #ff8b7f; --bad-bg: #3a1c19; --warn-bg: #3a2d12; --good-bg: #142e22;
    color-scheme: dark;
  }
}
:root[data-theme="dark"] {
  --bg: #0f161c; --surface: #17212a; --fg: #e5ebf0; --muted: #94a3b0; --line: #2a3945; --accent: #52b6d6;
  --good: #5ccf94; --warn: #f0b95a; --bad: #ff8b7f; --bad-bg: #3a1c19; --warn-bg: #3a2d12; --good-bg: #142e22;
  color-scheme: dark;
}
body { background: var(--bg); color: var(--fg); font-family: var(--sans); font-size: 15px; line-height: 1.5; }
.wrap { max-width: 1280px; margin: 0 auto; padding-block: 24px 48px; padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px)); }
h1 { font-size: 1.6rem; line-height: 1.2; margin: 0 0 4px; text-wrap: balance; }
.sub { color: var(--muted); margin: 0 0 20px; }
.strip { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; margin-bottom: 20px; }
.stat { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 10px 14px; min-width: 0; }
.stat b { display: block; font-size: 1.25rem; font-variant-numeric: tabular-nums; }
.stat span { color: var(--muted); font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.06em; }
.stat.good b { color: var(--good); } .stat.bad b { color: var(--bad); }
.verdict { border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; background: var(--good-bg); border: 1px solid var(--good); }
.verdict.bad { background: var(--bad-bg); border-color: var(--bad); }
.verdict ul { margin: 6px 0 0; padding-left: 20px; }
.filters { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; position: sticky; top: env(safe-area-inset-top, 0px); background: var(--bg); padding-block: 8px; z-index: 5; }
.filters button { font: inherit; font-size: 0.9rem; color: var(--fg); background: var(--surface); border: 1px solid var(--line); border-radius: 999px; padding: 4px 14px; cursor: pointer; }
.filters button[aria-pressed="true"] { background: var(--accent); border-color: var(--accent); color: var(--bg); }
.filters button:focus-visible, .shot:focus-visible, .close:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.group { margin: 28px 0 12px; font-size: 1.05rem; display: flex; gap: 10px; align-items: baseline; }
.group small { color: var(--muted); font-weight: 400; }
.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 300px), 1fr)); gap: 16px; }
.card { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; overflow: hidden; display: flex; flex-direction: column; min-width: 0; }
.card.flagged { border-color: var(--bad); }
.shot { all: unset; display: block; cursor: zoom-in; height: 210px; overflow: hidden; background: var(--line); }
.shot img { display: block; width: 100%; height: 100%; object-fit: cover; object-position: top left; }
.missing { height: 210px; display: grid; place-items: center; background: var(--warn-bg); color: var(--warn); padding: 16px; text-align: center; }
.body { padding: 12px 14px 14px; display: flex; flex-direction: column; gap: 6px; min-width: 0; }
.body h3 { margin: 0; font-size: 0.98rem; line-height: 1.3; }
.path { font-family: var(--mono); font-size: 0.78rem; color: var(--muted); overflow-wrap: anywhere; }
.look { margin: 0; font-size: 0.88rem; }
.note { margin: 0; font-size: 0.82rem; color: var(--muted); }
.tags { display: flex; flex-wrap: wrap; gap: 6px; }
.tag { font-size: 0.75rem; border-radius: 4px; padding: 1px 7px; background: var(--bad-bg); color: var(--bad); border: 1px solid var(--bad); overflow-wrap: anywhere; }
.tag.soft { background: var(--warn-bg); color: var(--warn); border-color: var(--warn); }
.tag.kind { background: transparent; color: var(--muted); border-color: var(--line); }
dialog { border: 0; padding: 0; background: transparent; max-width: 100vw; max-height: 100vh; width: 100%; height: 100%; }
dialog::backdrop { background: rgba(8, 12, 16, 0.82); }
.viewer { height: 100%; display: flex; flex-direction: column; padding: 12px; box-sizing: border-box; gap: 8px; }
.viewer header { display: flex; justify-content: space-between; align-items: center; gap: 12px; color: #fff; }
.close { font: inherit; background: var(--surface); color: var(--fg); border: 1px solid var(--line); border-radius: 6px; padding: 4px 14px; cursor: pointer; }
.scroll { flex: 1; overflow: auto; background: var(--surface); border-radius: 6px; }
.scroll img { display: block; width: 100%; max-width: 1400px; margin: 0 auto; }
@media (prefers-reduced-motion: no-preference) { .card { transition: border-color 0.15s; } }
</style>
<div class="wrap">
<h1>Plattarr UI Gallery</h1>
<p class="sub">__SUB__</p>
<div class="strip">__STRIP__</div>
__VERDICT__
<div class="filters" id="filters" role="group" aria-label="Sections"></div>
__SECTIONS__
</div>
<dialog id="viewer"><div class="viewer"><header><span id="vtitle"></span><button class="close" id="vclose" type="button">Close</button></header><div class="scroll"><img id="vimg" alt=""></div></div></dialog>
<script>
(function () {
  var cards = Array.prototype.slice.call(document.querySelectorAll('[data-group]'));
  var groups = [];
  cards.forEach(function (c) { if (groups.indexOf(c.dataset.group) < 0) groups.push(c.dataset.group); });
  var bar = document.getElementById('filters');
  function show(name) {
    document.querySelectorAll('[data-group]').forEach(function (s) { s.hidden = name !== 'All' && s.dataset.group !== name; });
    Array.prototype.forEach.call(bar.children, function (b) { b.setAttribute('aria-pressed', String(b.textContent === name)); });
  }
  ['All'].concat(groups).forEach(function (name) {
    var b = document.createElement('button');
    b.type = 'button'; b.textContent = name; b.onclick = function () { show(name); };
    bar.appendChild(b);
  });
  show('All');
  var dlg = document.getElementById('viewer');
  document.querySelectorAll('.shot').forEach(function (btn) {
    btn.onclick = function () {
      document.getElementById('vimg').src = btn.querySelector('img').src;
      document.getElementById('vtitle').textContent = btn.dataset.title;
      if (dlg.showModal) dlg.showModal(); else dlg.setAttribute('open', '');
    };
  });
  document.getElementById('vclose').onclick = function () { dlg.close(); };
  dlg.addEventListener('click', function (e) { if (e.target === dlg) dlg.close(); });
})();
</script>
"""


def esc(value):
    return html.escape(str(value), quote=True)


def card(shot, shots_dir):
    flagged = bool(shot.get("errors") or shot.get("bad") or shot.get("raw") or shot.get("dup"))
    tags = []
    if shot.get("kind"):
        tags.append(f'<span class="tag kind">{esc(shot["kind"])}</span>')
    tags += [f'<span class="tag">console: {esc(e)}</span>' for e in shot.get("errors", [])]
    tags += [f'<span class="tag">HTTP: {esc(b)}</span>' for b in shot.get("bad", [])]
    tags += [f'<span class="tag">raw key: {esc(r)}</span>' for r in shot.get("raw", [])]
    tags += [f'<span class="tag">heading twice: {esc(d)}</span>' for d in shot.get("dup", [])]
    tags += [f'<span class="tag soft">check: {esc(m)}</span>' for m in shot.get("maybe", [])[:4]]
    if shot.get("file"):
        with open(os.path.join(shots_dir, shot["file"]), "rb") as f:
            data = base64.b64encode(f.read()).decode("ascii")
        media = (f'<button class="shot" type="button" data-title="{esc(shot["title"])}" aria-label="Open {esc(shot["title"])} full size">'
                 f'<img src="data:image/jpeg;base64,{data}" alt="Screenshot: {esc(shot["title"])}" loading="lazy"></button>')
    else:
        media = '<div class="missing">Not captured</div>'
    note = f'<p class="note">{esc(shot["note"])}</p>' if shot.get("note") else ""
    return (f'<article class="card{" flagged" if flagged else ""}">{media}<div class="body"><h3>{esc(shot["title"])}</h3>'
            f'<div class="path">{esc(shot["path"])}</div><p class="look">{esc(shot.get("look", ""))}</p>{note}'
            f'<div class="tags">{"".join(tags)}</div></div></article>')


def build(directory):
    with open(os.path.join(directory, "manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    shots_dir = os.path.join(directory, "shots")
    shots = manifest["shots"]

    flagged = [s for s in shots if s.get("errors") or s.get("bad") or s.get("raw") or s.get("dup")]
    missing = [s for s in shots if not s.get("file")]
    maybe = sum(len(s.get("maybe", [])) for s in shots)

    strip = "".join([
        f'<div class="stat"><b>{len(shots)}</b><span>Screenshots</span></div>',
        f'<div class="stat {"bad" if flagged else "good"}"><b>{len(flagged)}</b><span>With findings</span></div>',
        f'<div class="stat {"bad" if missing else "good"}"><b>{len(missing)}</b><span>Not captured</span></div>',
        f'<div class="stat"><b>{esc(manifest.get("migration", "?"))}</b><span>Migration</span></div>',
    ])
    if flagged or missing:
        items = "".join(f"<li>{esc(s['title'])}: " + esc("; ".join(
            [f"console {e}" for e in s.get("errors", [])] + [f"HTTP {b}" for b in s.get("bad", [])] +
            [f"raw key {r}" for r in s.get("raw", [])] + [f"heading twice {d}" for d in s.get("dup", [])] +
            (["not captured: " + s.get("note", "")] if not s.get("file") else []))) + "</li>" for s in flagged + [m for m in missing if m not in flagged])
        verdict = f'<div class="verdict bad"><strong>Findings</strong><ul>{items}</ul></div>'
    else:
        verdict = ('<div class="verdict"><strong>No findings.</strong> No console error, failed request, untranslated key or duplicated section '
                   f'on any page{f"; {maybe} CamelCase words are listed as checks (logger names and similar)" if maybe else ""}.</div>')

    order, by_group = [], {}
    for s in shots:
        if s["group"] not in by_group:
            order.append(s["group"])
        by_group.setdefault(s["group"], []).append(s)
    sections = "".join(
        f'<section data-group="{esc(g)}"><h2 class="group">{esc(g)} <small>{len(by_group[g])}</small></h2>'
        f'<div class="grid">{"".join(card(s, shots_dir) for s in by_group[g])}</div></section>' for g in order)

    sub = f'{esc(manifest.get("app", ""))}, captured {esc(manifest.get("taken", ""))}. Click a screenshot to open it at full width.'
    if os.environ.get("GALLERY_NOTE"):   # what a reader needs to know about the instance that was photographed
        sub += " " + esc(os.environ["GALLERY_NOTE"])
    return PAGE.replace("__SUB__", sub).replace("__STRIP__", strip).replace("__VERDICT__", verdict).replace("__SECTIONS__", sections)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    directory = argv[1]
    out = argv[2] if len(argv) > 2 else os.path.join(directory, "gallery.html")
    page = build(directory)
    with open(out, "w", encoding="utf-8") as f:
        f.write(page)
    print(f"{out}  {len(page) / 1e6:.1f} MB")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
