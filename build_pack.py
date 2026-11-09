"""Zip a font mod folder (e.g. packs/GoblinMenuCyrillic) into a Thunderstore package.

Layout the package must have, so every mod manager puts it somewhere POG Fonts finds it and no two
font mods overwrite each other:

    manifest.json  icon.png  README.md  CHANGELOG.md      Thunderstore's files, at the root
    Mods/<Author>-<Name>/pogfont.json                    the font mod itself, in a folder of its own
    Mods/<Author>-<Name>/<font>.ttf
    Mods/<Author>-<Name>/OFL.txt                         (the font's license, if it has one)

Why: Gale unpacks files that are not under a known folder loose into Mods/ (two font mods would both
bring Mods/pogfont.json), but keeps paths under Mods/ as they are. r2modman puts the whole package
into Mods/<package>/. POG Fonts searches Mods/ recursively, so both end up found.

The zip is build/<Author>-<name>-<version>.zip, name and version from manifest.json.

usage: python build_pack.py <pack folder>
"""
import json
import struct
import sys
import zipfile
from pathlib import Path

ROOT_REQUIRED = ["manifest.json", "icon.png", "README.md"]


def fail(msg):
    sys.exit(f"[ERROR] {msg}")


def main():
    if len(sys.argv) < 2:
        fail(__doc__)
    folder = Path(sys.argv[1]).resolve()

    for name in ROOT_REQUIRED:
        if not (folder / name).is_file():
            fail(f"{name} missing in {folder}")
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
    png = (folder / "icon.png").read_bytes()
    if png[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", png[16:24]) != (256, 256):
        fail("icon.png must be a 256x256 PNG")
    if not any(d.startswith("Largo-POG_Fonts-") for d in manifest.get("dependencies", [])):
        print("[WARN] manifest.json does not depend on Largo-POG_Fonts; mod managers will not install POG Fonts with it")

    if (folder / "pogfont.json").exists():
        fail("pogfont.json is at the package root: Gale would unpack it loose into Mods/, where another "
             "font mod's pogfont.json replaces it. Move it to Mods/<Author>-<Name>/")
    descriptors = sorted(folder.glob("Mods/*/pogfont.json"))
    if not descriptors:
        fail("no Mods/<Author>-<Name>/pogfont.json in the package")
    for d in descriptors:
        desc = json.loads(d.read_text(encoding="utf-8-sig"))
        for key in ("name", "font", "target"):
            if not desc.get(key):
                fail(f"{d.relative_to(folder)} needs \"{key}\"")
        if not (d.parent / desc["font"]).is_file():
            fail(f"{d.relative_to(folder)} names {desc['font']}, which is not next to it")

    author = "Largo"
    out_dir = Path(__file__).resolve().parent / "build"
    out_dir.mkdir(exist_ok=True)
    zip_path = out_dir / f"{author}-{manifest['name']}-{manifest['version_number']}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(p for p in folder.rglob("*") if p.is_file()):
            z.write(f, f.relative_to(folder).as_posix())
    print(f"[OK] {zip_path}")
    for zi in zipfile.ZipFile(zip_path).infolist():
        print(f"     {zi.filename}  {zi.file_size:,} bytes")


if __name__ == "__main__":
    main()
