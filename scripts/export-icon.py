"""Render the canonical SVG with Chromium, then export multi-resolution Windows ICO.

Design-time only: Python + Pillow and installed Edge/Chrome are required.
The normal .NET build uses the committed PNG/ICO and does not run this script.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "PanBridge" / "Assets"
SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]


def find_browser(explicit: str | None) -> Path:
    candidates = [Path(explicit)] if explicit else [
        Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe",
        Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Google/Chrome/Application/chrome.exe",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise RuntimeError("Pass --browser with the path to an installed Edge/Chrome executable.")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--browser")
    args = parser.parse_args()
    svg = ASSETS / "PanBridge.svg"
    png = ASSETS / "PanBridge.png"
    ico = ASSETS / "PanBridge.ico"
    tree = ET.parse(svg)
    namespace = {"s": "http://www.w3.org/2000/svg"}
    assert tree.getroot().get("viewBox") == "0 0 1024 1024"
    assert not tree.findall(".//s:image", namespace), "SVG must not embed a raster image."
    assert not tree.findall(".//s:filter", namespace), "Icon geometry must not depend on filter effects."
    profile = ROOT / ".build-tools" / "icon-render-profile"
    profile.mkdir(parents=True, exist_ok=True)
    browser = find_browser(args.browser)
    command = [str(browser), "--headless=new", "--disable-gpu", "--no-first-run",
               "--no-default-browser-check", "--disable-extensions", "--hide-scrollbars",
               "--force-device-scale-factor=1", "--default-background-color=00000000",
               "--window-size=1024,1024", f"--user-data-dir={profile}",
               f"--screenshot={png}", svg.resolve().as_uri()]
    subprocess.run(command, check=True, capture_output=True, timeout=60)
    with Image.open(png) as rendered:
        image = rendered.convert("RGBA")
    assert image.size == (1024, 1024), f"Unexpected render dimensions: {image.size}"
    # The entire rectangular core of the rounded tile must have no alpha holes.
    alpha = image.getchannel("A")
    assert alpha.crop((242, 56, 782, 968)).getextrema() == (255, 255)
    assert alpha.crop((56, 242, 968, 782)).getextrema() == (255, 255)
    assert alpha.getpixel((0, 0)) == 0
    assert image.getpixel((512, 850)) == (40, 104, 232, 255)
    checked = 0
    for y in range(1024):
        center_y = min(max(y + 0.5, 240), 784)
        for x in range(1024):
            center_x = min(max(x + 0.5, 240), 784)
            distance_squared = (x + 0.5 - center_x) ** 2 + (y + 0.5 - center_y) ** 2
            value = alpha.getpixel((x, y))
            if distance_squared < 182 ** 2:
                assert value == 255, f"Interior alpha hole at {(x, y)}"
                checked += 1
            elif distance_squared > 186 ** 2:
                assert value == 0, f"Stray pixel outside the tile at {(x, y)}"
    image.save(ico, format="ICO", sizes=[(size, size) for size in SIZES])
    with Image.open(ico) as icon:
        assert icon.ico.sizes() == {(size, size) for size in SIZES}
    print(json.dumps({"svg": str(svg), "png": str(png), "ico": str(ico),
                      "sizes": SIZES, "opaque_interior": True, "interior_pixels_checked": checked,
                      "transparent_outer_corners": True, "no_stray_outer_pixels": True}))


if __name__ == "__main__":
    main()
