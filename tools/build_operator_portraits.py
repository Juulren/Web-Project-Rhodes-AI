"""Genera assets/data/operator-portraits.json: nombre del operador -> archivo de retrato.

RhodesAPI enlaza sus imágenes a ak.gamepress.gg, un dominio que ya no resuelve DNS.
Este script reconstruye el identificador interno de cada operador (char_XXX_nombre)
y verifica qué retrato existe en el repositorio público yuanyan3060/ArknightsGameResource.

Uso:  python -I tools/build_operator_portraits.py
"""
import json
import re
import unicodedata
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

RHODES_API = "https://rhodesapi.up.railway.app/api/operator"
CHARACTER_TABLE = "https://raw.githubusercontent.com/yuanyan3060/ArknightsGameResource/main/gamedata/excel/character_table.json"
PORTRAIT_BASE = "https://raw.githubusercontent.com/yuanyan3060/ArknightsGameResource/main/portrait/"
OUTPUT = Path(__file__).resolve().parent.parent / "assets" / "data" / "operator-portraits.json"
CHAR_ID = re.compile(r"char_\d+_[a-z0-9]+", re.IGNORECASE)


def fetch_json(url):
    with urllib.request.urlopen(url, timeout=120) as response:
        return json.load(response)


def normalize(text):
    ascii_text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]", "", ascii_text.lower())


def exists(filename):
    request = urllib.request.Request(PORTRAIT_BASE + filename + ".png", method="HEAD")
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status == 200
    except Exception:
        return False


def resolve(operator, by_appellation):
    links = " ".join(art.get("link", "") for art in operator.get("art") or [] if isinstance(art, dict))
    match = CHAR_ID.search(links)
    char_id = match.group(0).lower() if match else by_appellation.get(normalize(operator["name"]))
    if not char_id:
        return operator["name"], None
    # Retrato de Élite 2 si existe; si no, el de Élite 0/1.
    for suffix in ("_2", "_1"):
        if exists(char_id + suffix):
            return operator["name"], char_id + suffix
    return operator["name"], None


def main():
    operators = fetch_json(RHODES_API)
    table = fetch_json(CHARACTER_TABLE)
    by_appellation = {}
    for char_id, data in table.items():
        if char_id.startswith("char_"):
            by_appellation.setdefault(normalize(data.get("appellation") or ""), char_id)

    with ThreadPoolExecutor(max_workers=16) as pool:
        results = dict(pool.map(lambda op: resolve(op, by_appellation), operators))

    portraits = {name: file for name, file in sorted(results.items()) if file}
    missing = sorted(name for name, file in results.items() if not file)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps({"base": PORTRAIT_BASE, "portraits": portraits}, ensure_ascii=False, indent=1) + "\n",
                      encoding="utf-8")
    print(f"{len(portraits)} retratos resueltos, {len(missing)} sin retrato: {missing}")


if __name__ == "__main__":
    main()
