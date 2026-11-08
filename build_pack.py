"""Zip a font mod folder (e.g. packs/GoblinMenuCyrillic) into a Thunderstore package.

Every file in the folder goes to the root of the zip, as Thunderstore expects. The zip is
build/<author>-<name>-<version>.zip, with name and version from the folder's manifest.json.
Checks first what Thunderstore and POG Fonts would reject: a missing file, a bad icon size,
a pogfont.json whose font is not there.

usage: python build_pack.py <pack folder> [--author Largo]
"""
import json
import struct
import sys
import zipfile
from pathlib import Path

REQUIRED = ["manifest.json", "icon.png", "README.md", "pogfont.json"]


def fail(msg):
    sys.exit(f"[ERROR] {msg}")


def main():
    if len(sys.argv) < 2:
        fail(__doc__)
    folder = Path(sys.argv[1]).resolve()
    author = sys.argv[sys.argv.index("--author") + 1] if "--author" in sys.argv else "Largo"

    for name in REQUIRED:
        if not (folder / name).is_file():
            fail(f"{name} missing in {folder}")

    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
    png = (folder / "icon.png").read_bytes()
    if png[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", png[16:24]) != (256, 256):
        fail("icon.png must be a 256x256 PNG")
    if not any(d.startswith("Largo-POG_Fonts-") for d in manifest.get("dependencies", [])):
        print("[WARN] manifest.json does not depend on Largo-POG_Fonts; mod managers will not install POG Fonts with it")

    desc = json.loads((folder / "pogfont.json").read_text(encoding="utf-8-sig"))
    for key in ("name", "font", "target"):
        if not desc.get(key):
            fail(f"pogfont.json needs \"{key}\"")
    if not (folder / desc["font"]).is_file():
        fail(f"pogfont.json names {desc['font']}, which is not in {folder}")

    out_dir = Path(__file__).resolve().parent / "build"
    out_dir.mkdir(exist_ok=True)
    zip_path = out_dir / f"{author}-{manifest['name']}-{manifest['version_number']}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(folder.iterdir()):
            if f.is_file():
                z.write(f, f.name)
    print(f"[OK] {zip_path}")
    for zi in zipfile.ZipFile(zip_path).infolist():
        print(f"     {zi.filename}  {zi.file_size:,} bytes")


if __name__ == "__main__":
    main()
