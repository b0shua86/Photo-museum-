// Meshy text-to-3D hero-asset pipeline for Camera Obscura.
//   node tools/meshy-gen.mjs
// Reads the API key from ~/.meshy_key, generates each asset (preview -> refine),
// downloads the textured GLB into Assets/Resources/MeshyModels/, and writes a
// manifest. Per-asset failures are isolated; preview GLB is used if refine fails.
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import os from "node:os";
import path from "node:path";

const KEY = readFileSync(path.join(os.homedir(), ".meshy_key"), "utf8").trim();
const BASE = "https://api.meshy.ai/openapi/v2/text-to-3d";
const OUT = "/Users/miller/Desktop/camera-obscura/Assets/Resources/MeshyModels";
mkdirSync(OUT, { recursive: true });

const ASSETS = [
  { name: "bust_classical", prompt: "A classical white marble portrait bust of a noble Roman figure on a small round socle, neoclassical museum sculpture, smooth polished Carrara marble, highly detailed, symmetrical, single object, plain background" },
  { name: "statue_draped", prompt: "A neoclassical white marble statue of a standing draped female allegorical figure in a flowing toga, full figure on a low round plinth, polished Carrara marble museum sculpture, graceful pose, highly detailed, single object" },
  { name: "statue_seated", prompt: "A neoclassical white marble statue of a seated robed philosopher reading, draped robes, on a low base, polished Carrara marble museum sculpture, contemplative, highly detailed, single object" },
  { name: "fountain", prompt: "An ornate two-tier baroque marble fountain, carved round basin with scrollwork and a central finial, white marble with subtle gold accents, symmetrical centerpiece, highly detailed, single object" },
  { name: "column", prompt: "A single tall fluted Corinthian marble column with an ornate acanthus-leaf capital and an attic base, white marble, classical architecture, perfectly vertical, isolated single object, plain background" },
  { name: "chandelier", prompt: "An ornate gilded baroque chandelier, golden brass scrolled arms with candle lights and hanging crystal pendants, luxurious symmetrical hanging fixture, highly detailed, single object" },
  { name: "urn", prompt: "An ornate classical marble urn vase with carved acanthus relief and two handles on a square pedestal, white marble with gold trim, decorative symmetrical museum object, highly detailed, single object" },
  { name: "bench", prompt: "An elegant neoclassical museum gallery bench, tufted dark green leather seat on a carved gilded gold wood frame with cabriole legs, isolated single furniture object, plain background" },
];

const H = { "Authorization": `Bearer ${KEY}`, "Content-Type": "application/json" };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

async function post(body) {
  const r = await fetch(BASE, { method: "POST", headers: H, body: JSON.stringify(body) });
  const j = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(`POST ${r.status} ${JSON.stringify(j)}`);
  return j.result;
}
async function getTask(id) {
  const r = await fetch(`${BASE}/${id}`, { headers: H });
  return r.json();
}
async function waitFor(id, label) {
  for (let i = 0; i < 240; i++) {
    const t = await getTask(id);
    if (t.status === "SUCCEEDED") return t;
    if (t.status === "FAILED" || t.status === "CANCELED") throw new Error(`${label} ${t.status}: ${t.task_error?.message || ""}`);
    if (i % 4 === 0) log(`  ${label} ${t.status} ${t.progress ?? 0}%`);
    await sleep(8000);
  }
  throw new Error(`${label} timed out`);
}
async function download(url, dest) {
  const r = await fetch(url);
  if (!r.ok) throw new Error(`download ${r.status}`);
  const buf = Buffer.from(await r.arrayBuffer());
  writeFileSync(dest, buf);
  return buf.length;
}

async function makeOne(a) {
  try {
    log(`[${a.name}] preview submit`);
    const previewId = await post({ mode: "preview", prompt: a.prompt, art_style: "realistic", should_remesh: true, target_polycount: 25000 });
    const preview = await waitFor(previewId, `[${a.name}] preview`);

    let final = preview;
    try {
      log(`[${a.name}] refine submit`);
      const refineId = await post({ mode: "refine", preview_task_id: previewId, enable_pbr: true });
      final = await waitFor(refineId, `[${a.name}] refine`);
    } catch (e) {
      log(`[${a.name}] refine skipped (${e.message}) — using preview`);
    }

    const glb = final.model_urls?.glb || preview.model_urls?.glb;
    if (!glb) throw new Error("no glb url");
    const dest = path.join(OUT, `${a.name}.glb`);
    const bytes = await download(glb, dest);
    log(`[${a.name}] DONE -> ${dest} (${(bytes / 1e6).toFixed(2)} MB)`);
    return { name: a.name, ok: true, file: `${a.name}.glb`, bytes };
  } catch (e) {
    log(`[${a.name}] FAILED: ${e.message}`);
    return { name: a.name, ok: false, error: e.message };
  }
}

const results = await Promise.all(ASSETS.map(makeOne));
writeFileSync(path.join(OUT, "manifest.json"), JSON.stringify(results, null, 2));
const ok = results.filter((r) => r.ok);
log(`\n=== COMPLETE: ${ok.length}/${results.length} assets ===`);
for (const r of results) log(`  ${r.ok ? "OK  " : "FAIL"} ${r.name}${r.ok ? "" : "  " + r.error}`);
