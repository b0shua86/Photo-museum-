// Source Kyle Thompson's wing from his own Format portfolio, mapping each series
// gallery to the matching artwork slots, at full resolution. Private, attributed
// tribute display — not redistributed.
import { writeFileSync, mkdirSync } from "node:fs";

const OUT = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets/art/kyle-thompson";
mkdirSync(OUT, { recursive: true });
const UA = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) AppleWebKit/605.1.15 Safari/605.1.15";
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const GALLERIES = {
  lifestyle: "/lifestyle",
  plague: "/plague",
  ghost: "/ghosttown",
  spaces: "/the-spaces-in-between-2016-2017",
  open: "/open-stage",
  bio: "/biography",
  portfolio: "/",
};

async function galleryImages(path) {
  const r = await fetch("https://www.kylethompsonphotography.com" + path, { headers: { "User-Agent": UA } });
  const html = await r.text();
  // Keep the FULL signed URL (?fjkss=...) — without the signature the CDN 403s.
  const urls = [...html.matchAll(/https:\/\/format\.creatorcdn\.com\/[^"'\s)]+/gi)].map((m) => m[0]);
  const byKey = {};
  const order = [];
  for (const u of urls) {
    const noq = u.split("?")[0];
    const p = noq.split("/");
    if (p.length < 13) continue;
    const s = p[7].split(",");
    const cropW = +s[2], cropH = +s[3], outW = +s[4] || 0, outH = +s[5] || 0;
    if (!(cropW > 200 && cropH > 200)) continue;
    const guid = p[9], file = p[12];
    const key = guid + "|" + file;
    // Prefer the best-framed large variant (max of the smaller output dimension),
    // which avoids the thin banner crops the layout also generates.
    const score = Math.min(outW, outH);
    const cur = byKey[key];
    if (!cur) { byKey[key] = { url: u, outW, outH, cropW, cropH, score, guid, file }; order.push(key); }
    else if (score > cur.score) byKey[key] = { url: u, outW, outH, cropW, cropH, score, guid, file };
  }
  return order.map((k) => byKey[k]).filter((x) => x.score >= 450);
}

async function download(img, name) {
  const r = await fetch(img.url, { headers: { "User-Agent": UA, Referer: "https://www.kylethompsonphotography.com/" } });
  if (!r.ok) throw new Error("HTTP " + r.status);
  const buf = Buffer.from(await r.arrayBuffer());
  writeFileSync(`${OUT}/${name}.jpg`, buf);
  console.log(`  ${name}.jpg  <-  ${img.cropW}x${img.cropH}  (${(buf.length / 1e6).toFixed(2)} MB)`);
}

const pools = {};
for (const [k, path] of Object.entries(GALLERIES)) {
  pools[k] = await galleryImages(path);
  console.log(`gallery ${k} (${path}): ${pools[k].length} images`);
  await sleep(600);
}

// Map series galleries -> slot names. 365 has no gallery -> draw from portfolio.
const used = new Set();
const take = (pool, n, names) => {
  const out = [];
  for (const img of pool) {
    if (out.length >= n) break;
    const id = img.guid + "|" + img.file;
    if (used.has(id)) continue;
    used.add(id); out.push(img);
  }
  return out.map((img, i) => [img, names[i]]);
};

const plan = [
  ...take(pools.lifestyle, 3, ["life-01", "life-02", "life-03"]),
  ...take(pools.portfolio, 4, ["365-01", "365-02", "365-03", "365-04"]),
  ...take(pools.plague, 3, ["plague-01", "plague-02", "plague-03"]),
  ...take(pools.ghost, 4, ["ghost-01", "ghost-02", "ghost-03", "ghost-04"]),
  ...take(pools.spaces, 3, ["spaces-01", "spaces-02", "spaces-03"]),
  ...take(pools.open, 3, ["open-01", "open-02", "open-03"]),
  ...take(pools.bio.length ? pools.bio : pools.portfolio, 1, ["portrait"]),
];

console.log(`\nDownloading ${plan.length} images...`);
let ok = 0;
for (const [img, name] of plan) {
  try { await download(img, name); ok++; } catch (e) { console.log(`  ${name} FAILED: ${e.message}`); }
  await sleep(300);
}
console.log(`\nDone: ${ok}/${plan.length} Kyle Thompson images saved to ${OUT}`);
