"""Build a .unitypackage from this UPM package (no Unity needed).

Usage: python Tools~/build_unitypackage.py <output.unitypackage>

The files are imported under Assets/RTLLocalizationStudio. Folders ending
with '~' (docs, tools) are skipped, as Unity ignores them anyway.
"""
import hashlib, io, json, os, re, sys, tarfile, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = "Assets/RTLLocalizationStudio"
ROOT_GUID = hashlib.md5(b"RTLLocalizationStudio-root").hexdigest()
ROOT_META = "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % ROOT_GUID


def add(tar, name, data):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mtime = int(time.time())
    tar.addfile(info, io.BytesIO(data))


def guid_of(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        m = re.search(r"^guid:\s*([0-9a-f]{32})", f.read(), re.M)
    if not m:
        sys.exit("No guid in " + meta_path)
    return m.group(1)


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    out = sys.argv[1]
    count = 0
    with tarfile.open(out, "w:gz") as tar:
        add(tar, ROOT_GUID + "/asset.meta", ROOT_META.encode())
        add(tar, ROOT_GUID + "/pathname", TARGET.encode())
        for dirpath, dirs, files in os.walk(ROOT):
            dirs[:] = sorted(d for d in dirs if not d.endswith("~") and not d.startswith("."))
            for name in sorted(files):
                if name.endswith(".meta") or name.startswith("."):
                    continue
                full = os.path.join(dirpath, name)
                meta = full + ".meta"
                if not os.path.exists(meta):
                    sys.exit("Missing meta: " + full)
                rel = os.path.relpath(full, ROOT).replace(os.sep, "/")
                guid = guid_of(meta)
                with open(full, "rb") as f:
                    add(tar, guid + "/asset", f.read())
                with open(meta, "rb") as f:
                    add(tar, guid + "/asset.meta", f.read())
                add(tar, guid + "/pathname", (TARGET + "/" + rel).encode())
                count += 1
            for d in dirs:
                meta = os.path.join(dirpath, d) + ".meta"
                if not os.path.exists(meta):
                    sys.exit("Missing folder meta: " + meta)
                rel = os.path.relpath(os.path.join(dirpath, d), ROOT).replace(os.sep, "/")
                guid = guid_of(meta)
                with open(meta, "rb") as f:
                    add(tar, guid + "/asset.meta", f.read())
                add(tar, guid + "/pathname", (TARGET + "/" + rel).encode())
    with open(os.path.join(ROOT, "package.json"), encoding="utf-8") as f:
        version = json.load(f)["version"]
    print("Built %s (v%s, %d files)" % (out, version, count))


if __name__ == "__main__":
    main()
