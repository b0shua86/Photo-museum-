// Fill Erwin Olaf's wing (across from Kyle) from his own site. Private tribute.
import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync } from "node:fs";
const DIR = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets/content";
const ART = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets/art/erwin-olaf";
const UA = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) AppleWebKit/605.1.15";
mkdirSync(ART, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const pages = ["/art", "/", "/commissioned/editorial", "/commissioned/portraits", "/commissioned/cultural"];
const urls = new Set();
for (const p of pages) {
  try {
    const html = await (await fetch("https://www.erwinolaf.com" + p, { headers: { "User-Agent": UA } })).text();
    for (const m of html.matchAll(/https:\/\/www\.erwinolaf\.com\/static_live\/upload\/[^"']+_NL\.jpg/gi)) urls.add(m[0]);
  } catch {}
  await sleep(300);
}
console.log("unique Erwin Olaf images found:", urls.size);

const artists = JSON.parse(readFileSync(`${DIR}/artists.json`, "utf8"));
const a = artists.find((x) => x.id === "erwin-olaf");
const existing = new Set(existsSync(ART) ? readdirSync(ART) : []);
let n = 1, added = 0;
for (const u of urls) {
  if (added >= 22) break;
  let name; do { name = `e${String(n).padStart(2, "0")}.jpg`; n++; } while (existing.has(name));
  try {
    const r = await fetch(u, { headers: { "User-Agent": UA, Referer: "https://www.erwinolaf.com/art" } });
    if (!r.ok) continue;
    const buf = Buffer.from(await r.arrayBuffer());
    if (buf.length < 8000) continue;
    writeFileSync(`${ART}/${name}`, buf);
    existing.add(name);
    a.artworks.push({
      id: `erwin-olaf-e${added + 1}`,
      title: "Untitled",
      year: "",
      image: `art/erwin-olaf/${name}`,
      description: "From the staged, cinematic, often unsettling tableaux of Erwin Olaf — meticulous studio fictions of mood, period and the body.",
      series: null,
      credit: "© Erwin Olaf Estate",
      mature: false,
    });
    added++;
  } catch {}
  await sleep(200);
}
writeFileSync(`${DIR}/artists.json`, JSON.stringify(artists, null, 2));
console.log(`Added ${added} works to Erwin Olaf (now ${a.artworks.length}).`);
