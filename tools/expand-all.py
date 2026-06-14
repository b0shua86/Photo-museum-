#!/usr/bin/env python3
# Expand every wing toward TARGET works in the single-row hang, pulling genuine
# additional pictures by/of each artist from Wikimedia Commons (public-domain)
# and a web image search (everything else). Candidates are name-verified, junk-
# filtered, size-gated and perceptually de-duplicated against what's already in
# the wing, then appended to artists.json with attribution. Wings are processed
# in parallel; artists.json is checkpointed (under a lock) after each wing, so the
# run is restartable — re-running tops up only the wings still under TARGET.
#
#   python3 tools/expand-all.py [TARGET] [--only id1,id2] [--workers N]
import json, os, sys, re, io, time, threading, urllib.request, urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed
from PIL import Image
Image.MAX_IMAGE_PIXELS = None

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, "Assets/StreamingAssets")
ART = os.path.join(BASE, "art")
CJSON = os.path.join(BASE, "content/artists.json")
ARGS = sys.argv[1:]
TARGET = int(ARGS[0]) if ARGS and ARGS[0].isdigit() else 12
ONLY = set(ARGS[ARGS.index("--only") + 1].split(",")) if "--only" in ARGS else None
WORKERS = int(ARGS[ARGS.index("--workers") + 1]) if "--workers" in ARGS else 8
RELAX = "--relax" in ARGS   # widen copyright sources to all credible art domains

UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"
# Wikimedia asks for a descriptive UA with contact; it gets gentler rate-limiting.
COMMONS_UA = "CameraObscuraMuseum/1.0 (educational photography-museum build; contact: wearethelegion@gmail.com)"
NUDE_RE = re.compile(r"\b(nude|nudes|naked|akt|desnudo|undressed|torso)\b", re.I)
BLOCK_RE = re.compile(r"\b(signature|autograph|grave|tomb|tombe|plaque|monument|\bmap\b|stamp|postage|"
                      r"envelope|coin|medal|logo|coat of arms|historical marker|book ?cover|title ?page|"
                      r"diagram|memorial|museum exterior|exhibition view|installation view|poster|postcard|"
                      r"for sale|mug|t-?shirt|wikipedia logo|icon|sample|watermark|alamy|getty ?images|"
                      r"homage|hommage|inspired|inspiration|tribute|imitation|replica|parody|fan ?art|"
                      r"in the style|style of|catalog|catalogue|\bbook\b|\bcover\b|mockup|template|"
                      r"\bdead\b|\bdies\b|\bdied\b|obituary|premium high ?res|stock photo|royalty|"
                      r"famous for|interview|in conversation|biography|\bnews\b|how to|tutorial|"
                      r"\bquote|\bstore\b|\bshop\b|merch|profile of|review)\b", re.I)
BAD_DOM = re.compile(r"(gettyimages|alamy|shutterstock|istockphoto|dreamstime|123rf|depositphotos|"
                     r"stock\.adobe|adobe ?stock|pinterest|ebay|etsy|redbubble|amazon|walmart|"
                     r"aliexpress|fineartamerica|posterlounge|wikihow)", re.I)
CREDIBLE = re.compile(r"(wikimedia|wikipedia|moma\.org|metmuseum|artic\.edu|getty\.edu|gettymuseum|tate\.org|"
                      r"nga\.gov|si\.edu|guggenheim|sfmoma|pompidou|rmngp|nationalgalleries|vam\.ac\.uk|loc\.gov|"
                      r"mfa\.org|lacma|whitney\.org|icp\.org|christies|sothebys|phillips|bonhams|swanngalleries|"
                      r"artsy|mutualart|artnet|wikiart|fraenkelgallery|howardgreenberg|americansuburbx|"
                      r"1854\.photography|clevelandart|harvard|yale\.edu|princeton|nationalmediamuseum|britannica|"
                      r"royalacademy|npg\.org|degruyter|fondationhcb|henricartierbresson|magnumphotos|"
                      r"aperture|lensculture|mocp\.org|widewalls|monovisions|artblart|vivianmaier|"
                      r"thephotographersgallery|fraenkelgallery|atgetphotography|americansuburbx)", re.I)
STRONG_DOM = re.compile(r"(artsy|mutualart|artnet|christie|sotheby|phillips|bonhams|swann|wikiart|"
                        r"invaluable|artprice|barnebys|fraenkel|howardgreenberg|"
                        r"moma\.org|metmuseum|tate\.org|guggenheim|sfmoma|artic\.edu)", re.I)


def http(url, binary=False, tries=5, timeout=50, ref=None):
    delay = 2
    for i in range(tries):
        try:
            ua = COMMONS_UA if "wikimedia.org" in url else UA
            h = {"User-Agent": ua, "Accept-Encoding": "identity", "Accept-Language": "en-US,en;q=0.9"}
            if ref:
                h["Referer"] = ref
            with urllib.request.urlopen(urllib.request.Request(url, headers=h), timeout=timeout) as r:
                d = r.read()
                return d if binary else json.loads(d)
        except urllib.error.HTTPError as e:
            if e.code in (429, 503) and i < tries - 1:
                ra = e.headers.get("Retry-After")
                w = int(ra) if (ra and ra.isdigit()) else delay
                time.sleep(min(w, 20)); delay = min(delay * 2, 20); continue
            return b"" if binary else None
        except Exception:
            if i < tries - 1:
                time.sleep(delay); delay = min(delay * 2, 20); continue
            return b"" if binary else None
    return b"" if binary else None


def dhash(img, hs=8):
    g = img.convert("L").resize((hs + 1, hs), Image.LANCZOS); px = list(g.getdata()); b = 0
    for r in range(hs):
        for c in range(hs):
            b = (b << 1) | (1 if px[r * (hs + 1) + c] > px[r * (hs + 1) + c + 1] else 0)
    return b


def ahash(img, hs=8):
    g = img.convert("L").resize((hs, hs), Image.LANCZOS); px = list(g.getdata()); a = sum(px) / len(px); b = 0
    for p in px:
        b = (b << 1) | (1 if p >= a else 0)
    return b


def ham(a, b):
    return bin(a ^ b).count("1")


def name_tokens(name):
    n = re.sub(r"\(.*?\)", "", name)
    return [t for t in re.split(r"[\s\-]+", n.lower()) if len(t) > 2 and t.isalpha()]


def clean_title(t):
    t = re.sub(r"^File:", "", t)
    t = re.sub(r"\.(jpe?g|png|tiff?)$", "", t, flags=re.I)
    t = re.sub(r"\s*-\s*Google Art Project.*$", "", t, flags=re.I)
    t = re.sub(r"\s*\([^)]*\)\s*", " ", t)
    return re.sub(r"\s+", " ", t.replace("_", " ")).strip()[:90]


SITE_RE = re.compile(r"(mutualart|moma|pompidou|artsy|wikiart|artnet|christie|sotheby|phillips|gallery|"
                     r"museum|compare similar|for sale|\.com|\.org|\.net|pinterest|tumblr|flickr|quarterly)", re.I)


def web_title(t, name):
    parts = [p.strip() for p in re.split(r"\s*[|]\s*|\s+[–—]\s+", t) if p.strip()]
    nl = name.lower()
    keep = [p for p in parts if nl not in p.lower() and not SITE_RE.search(p) and len(p) > 2]
    return clean_title(keep[0]) if keep else clean_title(t)


def commons(name, limit=80):
    u = ("https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search"
         f"&gsrnamespace=6&gsrlimit={limit}&gsrsearch={urllib.parse.quote(name)}"
         "&prop=imageinfo&iiprop=url|extmetadata|size&iiurlwidth=1600")
    j = http(u)
    out = []
    if not j:
        return out
    for p in (j.get("query", {}).get("pages") or {}).values():
        ii = (p.get("imageinfo") or [{}])[0]
        if not ii:
            continue
        title = p.get("title", "")
        if not re.search(r"\.(jpe?g|png)$", title, re.I):
            continue
        meta = ii.get("extmetadata") or {}
        am = re.sub(r"<[^>]+>", "", (meta.get("Artist", {}) or {}).get("value", "") or "")
        desc = re.sub(r"<[^>]+>", "", (meta.get("ImageDescription", {}) or {}).get("value", "") or "")
        lic = re.sub(r"<[^>]+>", "", (meta.get("LicenseShortName", {}) or {}).get("value", "") or "")
        ym = re.search(r"\b(1[789]\d\d|20[0-2]\d)\b",
                       (meta.get("DateTimeOriginal", {}) or {}).get("value", "") or
                       (meta.get("DateTime", {}) or {}).get("value", "") or title)
        out.append(dict(title=title, hay=(title + " " + am).lower(), text=(title + " " + desc),
                        url=ii.get("thumburl") or ii.get("url"), w=ii.get("width", 0), h=ii.get("height", 0),
                        year=ym.group(1) if ym else "", lic=lic, src="commons", page=""))
    return out


def ddg(name):
    out = []
    for suff in ("", " photograph"):
        q = name + suff
        home = http("https://duckduckgo.com/?q=" + urllib.parse.quote(q) + "&iax=images&ia=images", binary=True)
        if not home:
            continue
        m = re.search(rb"vqd=[\"']([\d-]+)[\"']", home)
        if not m:
            continue
        j = http(f"https://duckduckgo.com/i.js?l=us-en&o=json&q={urllib.parse.quote(q)}&vqd={m.group(1).decode()}&f=,,,&p=1",
                 ref="https://duckduckgo.com/")
        if not j:
            continue
        for r in j.get("results", []):
            u = r.get("image")
            if not u:
                continue
            t = r.get("title", "") or ""
            out.append(dict(title=t, hay=t.lower(), text=t, url=u, w=r.get("width", 0), h=r.get("height", 0),
                            year="", lic="", src="web", page=r.get("url", "") or ""))
        time.sleep(0.6)
    return out


_lock = threading.Lock()
_log = open("/tmp/expand.log", "a")
_data = None


def out(*a):
    s = " ".join(str(x) for x in a)
    with _lock:
        print(s, flush=True); _log.write(s + "\n"); _log.flush()


def process(a, idx, ntotal):
    arts = a.get("artworks", [])
    need = TARGET - len(arts)
    if need <= 0:
        return 0
    folder = os.path.join(ART, a["id"])
    os.makedirs(folder, exist_ok=True)
    toks = name_tokens(a["name"])
    last = toks[-1] if toks else ""
    first = toks[0] if toks else ""
    rights = (a.get("rights") or "").lower()

    existing = []
    present = set(os.listdir(folder)) if os.path.isdir(folder) else set()
    for f in sorted(present):
        if f.lower().endswith((".jpg", ".jpeg", ".png")):
            try:
                im = Image.open(os.path.join(folder, f))
                existing.append((dhash(im), ahash(im)))
            except Exception:
                pass

    # Public-domain wings fill from Commons alone (no slow DDG round-trips); living
    # artists draw only from the web (auction/museum) sources.
    cands = commons(a["name"]) if rights == "public-domain" else ddg(a["name"])
    cands.sort(key=lambda c: 0 if c["src"] == "commons" else (1 if STRONG_DOM.search(c.get("page", "") + c["url"]) else 2))

    chosen = []
    seen = set()
    nfile = 1
    new_entries = []
    tried = 0
    for c in cands:
        if len(new_entries) >= need or tried > 60:
            break
        if not c["url"] or c["url"] in seen:
            continue
        seen.add(c["url"])
        if c["src"] == "commons":
            if not last or last not in c["hay"]:
                continue
        else:
            where = c.get("page", "") + c["url"]
            if BAD_DOM.search(where) or not last or last not in c["hay"]:
                continue
            if rights == "public-domain":
                if not ((first and first in c["hay"]) or CREDIBLE.search(where)):
                    continue
            elif RELAX:  # living artist, widened: any credible art domain or full name
                if not (CREDIBLE.search(where) or (first and first in c["hay"])):
                    continue
            else:  # living artist: only single-work auction/gallery pages
                if not STRONG_DOM.search(where):
                    continue
        if BLOCK_RE.search(c["text"]):
            continue
        if c["src"] == "commons" and ((c["w"] or 9999) < 640 or (c["h"] or 9999) < 640):
            continue
        tried += 1
        raw = http(c["url"], binary=True, ref=c.get("page"))
        if not raw or len(raw) < 12000:
            continue
        try:
            im = Image.open(io.BytesIO(raw)); im.load()
        except Exception:
            continue
        if min(im.size) < 600:
            continue
        d, ah = dhash(im), ahash(im)
        if any(ham(d, ed) <= 10 and ham(ah, ea) <= 12 for ed, ea in existing + chosen):
            continue
        while f"x{nfile:02d}.jpg" in present:
            nfile += 1
        fname = f"x{nfile:02d}.jpg"; present.add(fname)
        im = im.convert("RGB"); w, h = im.size; m = max(w, h)
        if m > 2200:
            im = im.resize((round(w * 2200 / m), round(h * 2200 / m)), Image.LANCZOS)
        try:
            im.save(os.path.join(folder, fname), "JPEG", quality=88)
        except Exception:
            continue
        chosen.append((d, ah))
        credit = (("Wikimedia Commons" + (" · " + c["lic"] if c["lic"] else "")) if c["src"] == "commons"
                  else ("Source: " + (re.search(r"https?://([^/]+)", c.get("page") or c["url"]).group(1)
                                      if re.search(r"https?://([^/]+)", c.get("page") or c["url"]) else "web")))
        new_entries.append(dict(
            id=f"{a['id']}-x{len(new_entries)+1}",
            title=(web_title(c["title"], a["name"]) if c["src"] == "web" else clean_title(c["title"])) or f"{a['name']} — untitled",
            year=c["year"], image=f"art/{a['id']}/{fname}",
            description=f"From the work of {a['name']}.", series=None, credit=credit,
            mature=bool(NUDE_RE.search(c["text"])),
        ))

    with _lock:
        # re-number ids against whatever is now present, then append + checkpoint
        base_n = sum(1 for w in a["artworks"] if re.search(r"-x\d+$", w["id"]))
        for k, e in enumerate(new_entries):
            e["id"] = f"{a['id']}-x{base_n + k + 1}"
        a["artworks"].extend(new_entries)
        json.dump(_data, open(CJSON, "w"), ensure_ascii=False, indent=2)
    out(f"[{idx}/{ntotal}] {a['id']} [{rights}] +{len(new_entries)} (now {len(a['artworks'])})")
    return len(new_entries)


def main():
    global _data
    _data = json.load(open(CJSON))
    todo = [a for a in _data if (ONLY is None or a["id"] in ONLY) and TARGET - len(a.get("artworks", [])) > 0]
    # Interleave Commons (public-domain) and web (copyright) wings so the two rate-
    # limited sources are exercised concurrently rather than back-to-back.
    from itertools import zip_longest
    pd = [a for a in todo if (a.get("rights") or "").lower() == "public-domain"]
    cp = [a for a in todo if (a.get("rights") or "").lower() != "public-domain"]
    todo = [x for pair in zip_longest(pd, cp) for x in pair if x is not None]
    out(f"\n=== expand start: {len(todo)} wings need work, TARGET={TARGET}, workers={WORKERS} ===")
    n = len(todo)
    total = 0
    with ThreadPoolExecutor(max_workers=WORKERS) as ex:
        futs = {ex.submit(process, a, i + 1, n): a for i, a in enumerate(todo)}
        for f in as_completed(futs):
            try:
                total += f.result()
            except Exception as e:
                out("  ERR", futs[f]["id"], repr(e))
    out(f"\nDONE. added {total}. total artworks now {sum(len(a['artworks']) for a in _data)}")


if __name__ == "__main__":
    main()
