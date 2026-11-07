import sys, os, UnityPy
data, outdir = sys.argv[1], sys.argv[2]
for f in ("resources.assets", "sharedassets0.assets"):
    env = UnityPy.load(os.path.join(data, f))
    for obj in env.objects:
        if obj.type.name == "Font":
            d = obj.read(); fd = bytes(d.m_FontData)
            if fd:
                p = os.path.join(outdir, f"{d.m_Name}.ttf"); open(p, "wb").write(fd); print(f, d.m_Name, len(fd))
