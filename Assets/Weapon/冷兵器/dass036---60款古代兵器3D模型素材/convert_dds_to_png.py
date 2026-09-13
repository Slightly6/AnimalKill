# -*- coding: utf-8 -*-
"""Convert the model's DDS textures to PNG for Unity, preserving base filenames
so Unity's FBX importer can auto-link them to the model's materials."""
import os
import sys
from PIL import Image

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tex")
DST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Unity_Assets", "AncientWeapons", "Textures")

os.makedirs(DST, exist_ok=True)

ok, fail = 0, []
for name in sorted(os.listdir(SRC)):
    if not name.lower().endswith(".dds"):
        continue
    src = os.path.join(SRC, name)
    dst = os.path.join(DST, os.path.splitext(name)[0] + ".png")
    try:
        with Image.open(src) as im:
            im = im.convert("RGBA")
            im.save(dst, "PNG", optimize=True)
        ok += 1
    except Exception as e:  # noqa: BLE001
        fail.append((name, repr(e)))

print(f"converted: {ok}")
if fail:
    print("FAILED:")
    for n, e in fail:
        print(f"  {n}: {e}")
    sys.exit(1)
