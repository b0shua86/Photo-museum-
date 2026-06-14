// Remove double/triplicate images across the museum: any artwork whose image
// file is byte-identical to one already seen is dropped (file deleted, entry
// removed). The first occurrence in data order is kept.
import { readFileSync, writeFileSync, existsSync, unlinkSync } from "node:fs";
import { createHash } from "node:crypto";
import path from "node:path";

const ROOT = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets";
const DIR = `${ROOT}/content`;
const artists = JSON.parse(readFileSync(`${DIR}/artists.json`, "utf8"));

const seen = new Map(); // md5 -> "artistId/file"
let removed = 0, kept = 0, missing = 0;
for (const a of artists) {
  const keep = [];
  for (const w of a.artworks) {
    const p = path.join(ROOT, w.image || "");
    if (!w.image || !existsSync(p)) { missing++; keep.push(w); continue; }
    const md5 = createHash("md5").update(readFileSync(p)).digest("hex");
    if (seen.has(md5)) {
      try { unlinkSync(p); } catch {}
      removed++;
    } else {
      seen.set(md5, `${a.id}/${path.basename(p)}`);
      keep.push(w); kept++;
    }
  }
  a.artworks = keep;
}

writeFileSync(`${DIR}/artists.json`, JSON.stringify(artists, null, 2));
console.log(`Deduped: kept ${kept}, removed ${removed} duplicate images, ${missing} entries had no file (kept as placeholders).`);
console.log(`Total artworks now: ${artists.reduce((s, a) => s + a.artworks.length, 0)}`);
