"""Read public MCHOSE frontend assets; never opens or writes a HID device."""
import re
import urllib.request
import sys
from urllib.parse import urljoin

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = "https://www.mchose.com.cn/"


def fetch(path):
    with urllib.request.urlopen(urljoin(BASE, path), timeout=45) as response:
        return response.read().decode("utf-8", errors="replace")


def snippets(text, pattern, width=200, limit=25):
    hits = list(re.finditer(pattern, text, re.I))
    print("Matches:", len(hits))
    for hit in hits[:limit]:
        print(text[max(0, hit.start()-width):hit.end()+width])


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1:
        text = fetch(sys.argv[1])
        if len(sys.argv) > 2 and sys.argv[2] == "--assets":
            for asset in sorted(set(re.findall(r'[\w./-]+\.js', text))):
                print(asset)
        else:
            snippets(text, sys.argv[2] if len(sys.argv) > 2 else "G87|sendReport|0x[dD][dD]|gif", 260, 35)
    else:
        html = fetch("")
        main = re.search(r'src="(/assets/main-[^"]+\.js)"', html)[1]
        print("Main:", main)
        text = fetch(main)
        snippets(text, r"G87|keyboardMusic|hidDebug|gif", 180, 20)
        assets = sorted(set(re.findall(r'[\w./-]+\.js', text)))
        print("Relevant asset names:")
        for asset in assets:
            if re.search(r"g87|light|gif|hid|keyboard|device|music", asset, re.I):
                print(asset)
