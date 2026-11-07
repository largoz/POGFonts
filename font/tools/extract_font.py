import sys, os, hashlib, UnityPy
data, name, out = sys.argv[1], sys.argv[2], sys.argv[3]
env = UnityPy.load(os.path.join(data, "resources.assets"))
for obj in env.objects:
    if obj.type.name == "Font":
        d = obj.read()
        if d.m_Name == name:
            fd = bytes(d.m_FontData)
            open(out, "wb").write(fd)
            print(f"wrote {out} {len(fd)} bytes sha256={hashlib.sha256(fd).hexdigest()[:16]}")
            break
