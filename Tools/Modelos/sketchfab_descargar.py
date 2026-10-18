# Descarga modelos de Sketchfab con el token del usuario (nunca se imprime ni se copia).
# Uso:
#   python -I sf_download.py check [conjunto]           → valida el token y lista formatos disponibles
#   python -I sf_download.py get <destino_raiz> [conjunto] → descarga el glTF de cada modelo a <raiz>/<slug>/
# Conjuntos: atx (por defecto), laptop.
import json, os, re, sys, urllib.request, zipfile

TOKEN_FILE = os.path.join(os.path.expanduser("~"), ".sketchfab_token", "sketchfab.txt")

SETS = {}
SETS["atx"] = [
    ("pc_case", "2869fb18575d49128a3115d42d5d3826"),            # Basic PC case — sousinho
    ("motherboard_components", "3bc94057328243d4b341a55f59160f8a"),  # MotherBoard + Components — DanielCardonaArt
    ("gpu_rtx3060ti", "b65c5c0807be4ece9a95090d1a1794bb"),      # RTX 3060 TI Low Poly — alexlh2003
    ("cpu_cooler", "672a0a74a98c452a862016bee99f3579"),         # CPU Cooler — bazyaev08
    ("ssd_m2_980pro", "1f386c8d782d4b208717bc64474b98b3"),      # Samsung 980 Pro NVMe — BlenderFace1
    ("fan_sl120", "5abd0d8e89ea4241b7216f4b6d5a2ca4"),          # Lian Li UNI FAN SL120 — DIEKO
]
SETS["laptop"] = [
    ("dell_latitude_5400", "c3dfc53a600e427c9db9be83aec9860d"), # Dell Latitude 5400 — Yolala3d
    ("sodimm_ddr4_8gb", "d5c9a2a4d15146889aa2e0b360ff00a2"),    # 8 GB SODIMM DDR4 — Cashierobz
    ("laptop_battery", "e463a4413af647a3b88b6c6d40a12e72"),     # Mac Laptop Battery — Graphitage
    ("laptop_fan", "0796a914f76944f8bb443dce83dc0c8a"),         # Laptop cooling fan — ponchoguy
]


def token():
    t = open(TOKEN_FILE, encoding="utf-8").read().strip()
    if not re.fullmatch(r"[A-Za-z0-9]{20,64}", t):
        sys.exit("El archivo del token no tiene el formato esperado (alfanumérico, 20-64 caracteres).")
    return t


def api(path, tok=None):
    headers = {"User-Agent": "Mozilla/5.0"}
    if tok:
        headers["Authorization"] = "Token " + tok
    req = urllib.request.Request("https://api.sketchfab.com/v3" + path, headers=headers)
    with urllib.request.urlopen(req, timeout=40) as r:
        return json.load(r)


def info(uid):
    m = api("/models/" + uid)
    return {
        "name": m.get("name"),
        "author": (m.get("user") or {}).get("displayName") or (m.get("user") or {}).get("username"),
        "author_url": (m.get("user") or {}).get("profileUrl"),
        "license": (m.get("license") or {}).get("label"),
        "license_url": (m.get("license") or {}).get("url"),
        "url": "https://sketchfab.com/3d-models/" + uid,
        "faces": m.get("faceCount"),
    }


def main():
    mode = sys.argv[1]
    global MODELS
    MODELS = SETS[sys.argv[2] if mode == "check" and len(sys.argv) > 2 else sys.argv[3] if len(sys.argv) > 3 else "atx"]
    tok = token()
    if mode == "check":
        me = api("/me", tok)
        print("token válido; cuenta:", me.get("username"))
        for slug, uid in MODELS:
            d = api("/models/%s/download" % uid, tok)
            fmts = ", ".join("%s %.1f MB" % (k, (v or {}).get("size", 0) / 1e6) for k, v in d.items() if isinstance(v, dict))
            print("%-24s %s" % (slug, fmts))
        return

    root = sys.argv[2]
    os.makedirs(root, exist_ok=True)
    credits = []
    for slug, uid in MODELS:
        dest = os.path.join(root, slug)
        os.makedirs(dest, exist_ok=True)
        d = api("/models/%s/download" % uid, tok)
        url = (d.get("gltf") or {}).get("url")
        if not url:
            print("SIN glTF:", slug)
            continue
        zpath = os.path.join(dest, slug + "_gltf.zip")
        with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=300) as r, open(zpath, "wb") as f:
            f.write(r.read())
        with zipfile.ZipFile(zpath) as z:
            for member in z.namelist():  # no extraer fuera de la carpeta destino
                target = os.path.realpath(os.path.join(dest, "gltf", member))
                if not target.startswith(os.path.realpath(os.path.join(dest, "gltf"))):
                    sys.exit("Ruta sospechosa en el zip: " + member)
            z.extractall(os.path.join(dest, "gltf"))
        meta = info(uid)
        meta["slug"] = slug
        json.dump(meta, open(os.path.join(dest, "licencia.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        credits.append(meta)
        print("ok %-24s %.1f MB  %s — %s (%s)" % (slug, os.path.getsize(zpath) / 1e6, meta["name"], meta["author"], meta["license"]))
    json.dump(credits, open(os.path.join(root, "creditos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


main()
