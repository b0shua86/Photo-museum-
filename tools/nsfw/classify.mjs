// Image-based nudity flagging. Runs nsfwjs over every artwork image and sets
// `mature` so the in-app toggle (default hidden) is meaningful even for untitled
// nudes. Falls back gracefully: only flips flags it is confident about.
import "./util-polyfill.mjs";
import * as tf from "@tensorflow/tfjs-node";
import * as nsfw from "nsfwjs";
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import path from "node:path";

const ROOT = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets";
const DIR = `${ROOT}/content`;
const artists = JSON.parse(readFileSync(`${DIR}/artists.json`, "utf8"));

console.log("loading nsfw model…");
const model = await nsfw.load(); // MobileNetV2
console.log("model loaded.");

let scanned = 0, flagged = 0;
for (const a of artists) {
  for (const w of a.artworks) {
    const p = path.join(ROOT, w.image || "");
    if (!w.image || !existsSync(p)) continue;
    try {
      const img = tf.node.decodeImage(readFileSync(p), 3);
      const preds = await model.classify(img);
      img.dispose();
      const m = Object.fromEntries(preds.map((x) => [x.className, x.probability]));
      const score = (m.Porn || 0) + (m.Hentai || 0) + 0.6 * (m.Sexy || 0);
      const mature = score > 0.5 || (m.Porn || 0) > 0.3;
      w.mature = !!mature;
      if (mature) flagged++;
      scanned++;
      if (scanned % 60 === 0) console.log(`  …${scanned} scanned, ${flagged} mature`);
    } catch (e) { /* keep existing flag */ }
  }
}

writeFileSync(`${DIR}/artists.json`, JSON.stringify(artists, null, 2));
console.log(`Done. Scanned ${scanned} images, flagged ${flagged} as mature.`);
