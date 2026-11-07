import sys, os, UnityPy
data = sys.argv[1]
files = sorted(f for f in os.listdir(data) if f.endswith(".assets") or f.startswith("level") or f == "globalgamemanagers")
for f in files:
    p = os.path.join(data, f)
    try:
        env = UnityPy.load(p)
    except Exception as e:
        print(f"{f}: load failed {e!r}"); continue
    for obj in env.objects:
        t = obj.type.name
        if t not in ("Font", "MonoBehaviour"): continue
        try:
            name = obj.peek_name()
        except Exception:
            name = None
        if t == "MonoBehaviour":
            if not name or "SDF" not in name: continue
            print(f"{f}\tTMP\t{obj.path_id}\t{name}")
        else:
            try:
                d = obj.read()
                fd = bytes(d.m_FontData) if getattr(d, "m_FontData", None) else b""
                print(f"{f}\tFont\t{obj.path_id}\t{d.m_Name}\tfontdata={len(fd)} magic={fd[:4]!r}")
            except Exception as e:
                print(f"{f}\tFont\t{obj.path_id}\t{name}\tread failed {e!r}")
