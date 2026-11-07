"""Met à jour repo.json et dist/latest.zip après une compilation (utilisé par GitHub Actions)."""
import json
import shutil
import sys
import time

build = "PartagePropre/bin/Release/PartagePropre"
repo = sys.argv[1] if len(sys.argv) > 1 else "ekilliox/Clean-Share"
url = f"https://raw.githubusercontent.com/{repo}/main/dist/latest.zip"

shutil.copyfile(f"{build}/latest.zip", "dist/latest.zip")
m = json.load(open(f"{build}/PartagePropre.json", encoding="utf-8"))
m.update({
    "DownloadLinkInstall": url,
    "DownloadLinkUpdate": url,
    "DownloadLinkTesting": url,
    "IsHide": False,
    "IsTestingExclusive": False,
    "AcceptsFeedback": False,
    "LastUpdate": int(time.time()),
})
with open("repo.json", "w", encoding="utf-8") as f:
    json.dump([m], f, ensure_ascii=False, indent=2)
print("Version publiée :", m["AssemblyVersion"])
