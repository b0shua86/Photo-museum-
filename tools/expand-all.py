#!/usr/bin/env python3
# Expand every wing toward TARGET works in the single-row hang, pulling genuine
# additional pictures by/of each artist from Wikimedia Commons (public-domain)
# and a web image search (everything else). Candidates are name-verified, junk-
# filtered, size-gated and perceptually de-duplicated against what's already in
# the wing, then appended to artists.json with attribution. Idempotent: wings
# already at/over TARGET are skipped, and artists.json is checkpointed per wing.
#
#   python3 tools/expand-all.py [TARGET] [--only id1,id2]
import json, os, sys, re, io, time, html, urllib.request, urllib.parse
from PIL import Image
Image.MAX_IMAGE_PIXELS = None

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, "Assets/StreamingAssets")
ART = os.path.join(BASE, "art")
CJSON = os.path.join(BASE, "content/artists.json")
TARGET = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 12
ONLY = None
if "--only" in sys.argv:
    ONLY = set(sys.argv[sys.argv.index("--only") + 1].split(","))

UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"
NUDE_RE = re.compile(r"\b(nude|nudes|naked|akt|desnudo|undressed|torso)\b", re.I)
BLOCK_RE = re.compile(r"\b(signature|autograph|grave|tomb|tombe|plaque|monument|\bmap\b|stamp|postage|"
                      r"envelope|coin|medal|logo|coat of arms|historical marker|book ?cover|title ?page|"
                      r"diagram|memorial|museum exterior|exhibition view|installation view|poster|postcard|"
                      r"for sale|mug|t-?shirt|wikipedia logo|icon|sample|watermark|alamy|getty ?images|"
                      # imitations / merch / printed matter — NOT the artist's own pictures
                      r"homage|hommage|inspired|inspiration|tribute|imitation|replica|parody|fan ?art|"
                      r"in the style|style of|catalog|catalogue|\bbook\b|\bcover\b|mockup|template|"
                      # news / obituary / stock / merch / editorial — not the artist's pictures
                      r"\bdead\b|\bdies\b|\bdied\b|obituary|premium high ?res|stock photo|royalty|"
                      r"famous for|interview|in conversation|biography|\bnews\b|how to|tutorial|"
                      r"\bquote|\bstore\b|\bshop\b|merch|for sale|profile of|review)\b", re.I)
# Stock/marketplace/social domains never hold an artist's authoritative works.
BAD_DOM = re.compile(r"(gettyimages|alamy|shutterstock|istockphoto|dreamstime|123rf|depositphotos|"
                     r"stock\.adobe|adobe ?stock|pinterest|ebay|etsy|redbubble|amazon|walmart|"
                     r"aliexpress|fineartamerica|posterlounge|wikihow)", re.I)
# Auction/gallery/market pages list a SINGLE work with a clean reproduction — the
# best source of individual pictures by living artists (museum/article pages tend
# to show installation views and portrait grids instead).
STRONG_DOM = re.compile(r"(artsy|mutualart|artnet|christie|sotheby|phillips|bonhams|swann|wikiart|"
                        r"gagosian|fraenkel|howardgreenberg|hauserwirth|davidzwirner|pacegallery|"
                        r"mariangoodman|spruth|sprueth|moma\.org|metmuseum|tate\.org|guggenheim|"
                        r"sfmoma|artic\.edu)", re.I)
CREDIBLE = re.compile(r"(wikimedia|wikipedia|moma\.org|metmuseum|artic\.edu|getty\.edu|gettymuseum|tate\.org|nga\.gov|si\.edu|"
                      r"guggenheim|sfmoma|pompidou|rmngp|nationalgalleries|vam\.ac\.uk|loc\.gov|mfa\.org|lacma|"
                      r"whitney\.org|icp\.org|christies|sothebys|phillips|bonhams|swanngalleries|artsy|mutualart|"
                      r"artnet|wikiart|fraenkelgallery|howardgreenberg|americansuburbx|1854\.photography|"
                      r"clevelandart|harvard|yale\.edu|princeton|nationalmediamuseum|britannica|royalacademy|"
                      r"npg\.org|moma|reading\.ac\.uk|degruyter|fondationhcb|henricartierbresson|magnumphotos)", re.I)


def http(url, binary=False, tries=6, timeout=60, ref=None):
    delay = 3
    for i in range(tries):
        try:
            h = {"User-Agent": UA, "Accept-Encoding": "identity", "Accept-Language": "en-US,en;q=0.9"}
            if ref:
                h["Referer"] = ref
            with urllib.request.urlopen(urllib.request.Request(url, headers=h), timeout=timeout) as r:
                d = r.read()
                return d if binary else json.loads(d)
        except urllib.error.HTTPError as e:
            if e.code in (429, 503) and i < tries - 1:
                ra = e.headers.get("Retry-After")
                w = int(ra) if (ra and ra.isdigit()) else delay
                time.sleep(min(w, 40)); delay = min(delay * 2, 40); continue
            return b"" if binary else None
        except Exception:
            if i < tries - 1:
                time.sleep(delay); delay = min(delay * 2, 40); continue
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
    toks = [t for t in re.split(r"[\s\-]+", n.lower()) if len(t) > 2 and t.isalpha()]
    return toks


def clean_title(t):
    t = re.sub(r"^File:", "", t)
    t = re.sub(r"\.(jpe?g|png|tiff?)$", "", t, flags=re.I)
    t = re.sub(r"\s*-\s*Google Art Project.*$", "", t, flags=re.I)
    t = re.sub(r"\s*\([^)]*\)\s*", " ", t)
    t = t.replace("_", " ")
    return re.sub(r"\s+", " ", t).strip()[:90]


SITE_RE = re.compile(r"(mutualart|moma|pompidou|artsy|wikiart|artnet|christie|sotheby|phillips|gallery|"
                     r"museum|compare similar|for sale|\.com|\.org|\.net|pinterest|tumblr|flickr)", re.I)


def web_title(t, name):
    # Web results read like "Andreas Gursky | Montparnasse | MutualArt" — keep the
    # middle segment that is neither the artist's name nor a site/UI word.
    parts = [p.strip() for p in re.split(r"\s*[|]\s*|\s+[–—]\s+", t) if p.strip()]
    nl = name.lower()
    keep = [p for p in parts if nl not in p.lower() and not SITE_RE.search(p) and len(p) > 2]
    return clean_title(keep[0]) if keep else clean_title(t)


def commons(name, limit=60):
    out = []
    for q in (name, name + " photograph"):
        u = ("https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search"
             f"&gsrnamespace=6&gsrlimit={limit}&gsrsearch={urllib.parse.quote(q)}"
             "&prop=imageinfo&iiprop=url|extmetadata|size&iiurlwidth=1600")
        j = http(u)
        if not j:
            continue
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
                            year=ym.group(1) if ym else "", lic=lic, src="commons"))
        time.sleep(1.5)
    return out


def ddg(name, extra=("photograph", "photography")):
    out = []
    for suff in ("",) + tuple(" " + e for e in extra):
        q = name + suff
        home = http("https://duckduckgo.com/?q=" + urllib.parse.quote(q) + "&iax=images&ia=images", binary=True)
        if not home:
            continue
        m = re.search(rb"vqd=[\"']([\d-]+)[\"']", home)
        if not m:
            continue
        vqd = m.group(1).decode()
        j = http(f"https://duckduckgo.com/i.js?l=us-en&o=json&q={urllib.parse.quote(q)}&vqd={vqd}&f=,,,&p=1",
                 ref="https://duckduckgo.com/")
        if not j:
            continue
        for r in j.get("results", []):
            u = r.get("image"); t = r.get("title", "") or ""; pg = r.get("url", "") or ""
            if not u:
                continue
            out.append(dict(title=t, hay=t.lower(), text=t, url=u, w=r.get("width", 0), h=r.get("height", 0),
                            year="", lic="", src="web", page=pg))
        time.sleep(2)
    return out


def main():
    data = json.load(open(CJSON))
    log = open("/tmp/expand.log", "a")

    def out(*a):
        s = " ".join(str(x) for x in a)
        print(s, flush=True); log.write(s + "\n"); log.flush()

    total_added = 0
    for ai, a in enumerate(data):
        if ONLY and a["id"] not in ONLY:
            continue
        arts = a.setdefault("artworks", [])
        need = TARGET - len(arts)
        if need <= 0:
            continue
        folder = os.path.join(ART, a["id"])
        os.makedirs(folder, exist_ok=True)
        toks = name_tokens(a["name"])
        last = toks[-1] if toks else ""
        first = toks[0] if toks else ""

        # hashes of everything already in the wing
        existing = []
        present = set(os.listdir(folder)) if os.path.isdir(folder) else set()
        for f in sorted(present):
            if f.lower().endswith((".jpg", ".jpeg", ".png")):
                try:
                    existing.append((dhash(Image.open(os.path.join(folder, f))),
                                     ahash(Image.open(os.path.join(folder, f)))))
                except Exception:
                    pass

        rights = (a.get("rights") or "").lower()
        out(f"\n[{ai+1}/{len(data)}] {a['id']} ({a['name']}) [{rights}] have={len(arts)} need={need}")
        if rights == "public-domain":
            cands = commons(a["name"]) + ddg(a["name"])
        else:
            # For living/copyright artists Commons mostly holds homages and merch;
            # pull their actual works from credible art sites via web search only.
            cands = ddg(a["name"])

        # rank: commons first, then credible-domain web, then the rest
        def rank(c):
            return (0 if c["src"] == "commons" else (1 if CREDIBLE.search(c.get("page", "") + c["url"]) else 2))
        cands.sort(key=rank)

        chosen = []
        seen_urls = set()
        nfile = 1
        added = 0
        for c in cands:
            if added >= need:
                break
            if c["url"] in seen_urls:
                continue
            seen_urls.add(c["url"])
            if not re.search(r"\.(jpe?g|png)(\?|$)", c["url"], re.I) and c["src"] == "web":
                continue
            # name verification
            if c["src"] == "commons":
                if not last or last not in c["hay"]:
                    continue
            else:  # web
                where = c.get("page", "") + c["url"]
                if BAD_DOM.search(where) or last not in c["hay"]:
                    continue
                if rights == "public-domain":
                    full = bool(first and first in c["hay"])
                    if not (full or CREDIBLE.search(where)):
                        continue
                else:  # living artist: only single-work auction/gallery pages
                    if not STRONG_DOM.search(where):
                        continue
            if BLOCK_RE.search(c["text"]):
                continue
            if (c["w"] or 9999) < 640 or (c["h"] or 9999) < 640:
                # width/height may be 0 from ddg; verify after download
                if c["src"] == "commons":
                    continue
            try:
                raw = http(c["url"], binary=True, ref=c.get("page"))
                if not raw or len(raw) < 12000:
                    continue
                im = Image.open(io.BytesIO(raw)); im.load()
            except Exception:
                continue
            if min(im.size) < 600:
                continue
            d, ah = dhash(im), ahash(im)
            if any(ham(d, ed) <= 10 and ham(ah, ea) <= 12 for ed, ea in existing):
                continue
            if any(ham(d, cd) <= 10 and ham(ah, ca) <= 12 for cd, ca in chosen):
                continue
            # write
            while f"x{nfile:02d}.jpg" in present:
                nfile += 1
            fname = f"x{nfile:02d}.jpg"; present.add(fname)
            im = im.convert("RGB"); w, h = im.size; m = max(w, h)
            if m > 2200:
                im = im.resize((round(w * 2200 / m), round(h * 2200 / m)), Image.LANCZOS)
            im.save(os.path.join(folder, fname), "JPEG", quality=88)
            chosen.append((d, ah))
            credit = (("Wikimedia Commons" + (" · " + c["lic"] if c["lic"] else ""))
                      if c["src"] == "commons" else
                      ("Source: " + (re.search(r"https?://([^/]+)", c.get("page") or c["url"]).group(1)
                                     if re.search(r"https?://([^/]+)", c.get("page") or c["url"]) else "web")))
            xn = sum(1 for w0 in arts if w0["id"].startswith(a["id"] + "-x")) + 1
            arts.append(dict(
                id=f"{a['id']}-x{xn}",
                title=(web_title(c["title"], a["name"]) if c["src"] == "web" else clean_title(c["title"]))
                      or f"{a['name']} — untitled",
                year=c["year"],
                image=f"art/{a['id']}/{fname}",
                description=f"From the work of {a['name']}.",
                series=None,
                credit=credit,
                mature=bool(NUDE_RE.search(c["text"])),
            ))
            added += 1; total_added += 1
            out(f"   + {fname}  {im.size[0]}x{im.size[1]}  [{c['src']}]  {clean_title(c['title'])[:56]}")
            time.sleep(0.3)
        out(f"   => added {added} (now {len(arts)})")
        # checkpoint after every wing
        json.dump(data, open(CJSON, "w"), ensure_ascii=False, indent=2)
    out(f"\nDONE. total added {total_added}. total artworks now "
        f"{sum(len(x['artworks']) for x in data)}")
    log.close()


if __name__ == "__main__":
    main()
