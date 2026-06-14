// Expand wings with large available bodies of work: for each public-domain
// artist, pull additional images from Wikimedia Commons (titled/credited to that
// artist), download them, flag nudity, and append new artwork entries directly
// into artists.json. Existing images are kept; new ones are named e01.jpg…
//
//   node tools/expand-art.mjs [maxPerArtist]
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync } from "node:fs";

const DIR = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets/content";
const ARTROOT = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets/art";
const MAXP = +(process.argv[2] || 12);
const UA = "CameraObscuraMuseum/1.0 (private study project; contact: local)";
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const NUDE_RE = /\b(nude|nudes|naked|nu|akt|desnudo|undressed|bare\s+(body|skin)|torso)\b/i;

const artists = JSON.parse(readFileSync(`${DIR}/artists.json`, "utf8"));

async function commons(query) {
  const u = `https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search` +
    `&gsrnamespace=6&gsrlimit=40&gsrsearch=${encodeURIComponent(query)}` +
    `&prop=imageinfo&iiprop=url|extmetadata&iiurlwidth=1800&origin=*`;
  const r = await fetch(u, { headers: { "User-Agent": UA } });
  if (!r.ok) return [];
  const j = await r.json();
  const pages = j?.query?.pages ? Object.values(j.query.pages) : [];
  return pages;
}

function lastName(name) {
  const parts = name.replace(/\(.*?\)/g, "").trim().split(/\s+/);
  return parts[parts.length - 1].toLowerCase();
}

async function download(url, dest) {
  const r = await fetch(url, { headers: { "User-Agent": UA } });
  if (!r.ok) throw new Error("HTTP " + r.status);
  const buf = Buffer.from(await r.arrayBuffer());
  if (buf.length < 8000) throw new Error("too small");
  writeFileSync(dest, buf);
  return buf.length;
}

let totalAdded = 0;
for (const a of artists) {
  if (a.rights !== "public-domain") continue;
  const dir = `${ARTROOT}/${a.id}`;
  mkdirSync(dir, { recursive: true });
  const existing = new Set(existsSync(dir) ? readdirSync(dir) : []);
  const ln = lastName(a.name);

  let pages;
  try { pages = await commons(a.name); } catch { pages = []; }
  await sleep(400);

  // Keep files plausibly BY/OF this artist: last name in title or Artist metadata.
  const cand = [];
  for (const p of pages) {
    const ii = p.imageinfo?.[0]; if (!ii) continue;
    const title = (p.title || "").replace(/^File:/, "");
    if (!/\.(jpe?g|png)$/i.test(title)) continue;
    const meta = ii.extmetadata || {};
    const artistMeta = (meta.Artist?.value || "").replace(/<[^>]+>/g, "");
    const desc = (meta.ImageDescription?.value || "").replace(/<[^>]+>/g, "");
    const hay = `${title} ${artistMeta}`.toLowerCase();
    if (!hay.includes(ln)) continue;
    const url = ii.thumburl || ii.url;
    if (!url) continue;
    const yearM = (meta.DateTimeOriginal?.value || meta.DateTime?.value || "").match(/\b(1[89]\d\d|20[0-2]\d)\b/);
    cand.push({
      url, title,
      year: yearM ? yearM[1] : "",
      mature: NUDE_RE.test(`${title} ${desc}`),
      credit: "Wikimedia Commons" + (meta.LicenseShortName?.value ? " · " + meta.LicenseShortName.value.replace(/<[^>]+>/g, "") : ""),
    });
  }

  if (cand.length === 0) { continue; }
  a.artworks = a.artworks || [];
  let n = 1, added = 0;
  for (const c of cand) {
    if (added >= MAXP) break;
    let name;
    do { name = `e${String(n).padStart(2, "0")}.jpg`; n++; } while (existing.has(name));
    const dest = `${dir}/${name}`;
    try { await download(c.url, dest); } catch { continue; }
    existing.add(name);
    a.artworks.push({
      id: `${a.id}-e${added + 1}`,
      title: c.title.replace(/\.(jpe?g|png)$/i, "").replace(/_/g, " ").slice(0, 90),
      year: c.year,
      image: `art/${a.id}/${name}`,
      description: `From the work of ${a.name}.`,
      series: null,
      credit: c.credit,
      mature: c.mature,
    });
    added++; totalAdded++;
    await sleep(250);
  }
  if (added) console.log(`  ${a.id.padEnd(22)} +${added} (now ${a.artworks.length})`);
}

writeFileSync(`${DIR}/artists.json`, JSON.stringify(artists, null, 2));
console.log(`\nDone. Added ${totalAdded} new artworks across public-domain wings.`);
