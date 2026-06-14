// Generate BLANK gold plaques (no text) for engraved-text overlays.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import os from "node:os";
import path from "node:path";

const KEY = readFileSync(path.join(os.homedir(), ".meshy_key"), "utf8").trim();
const BASE = "https://api.meshy.ai/openapi/v2/text-to-3d";
const OUT = "/Users/miller/Desktop/camera-obscura/Assets/Resources/MeshyModels";
mkdirSync(OUT, { recursive: true });

const ASSETS = [
  { name: "plaque_label", prompt: "A blank rectangular museum wall label plaque, polished gold brass metal with an ornate engraved beveled border frame, completely smooth flat blank center panel with NO text and no letters, landscape orientation, thin, wall-mounted, single object, plain background" },
  { name: "plaque_cartouche", prompt: "A large ornate baroque gold cartouche wall plaque, gilded scrollwork and acanthus border around a smooth flat blank polished center panel with NO text and no letters, symmetrical decorative gold tablet, landscape orientation, wall-mounted, single object, plain background" },
  { name: "plaque_tall", prompt: "An ornate gold-framed museum information plaque, gilded carved gold border around a smooth blank ivory cream stone panel with NO text and no letters, portrait orientation, elegant, wall-mounted, single object, plain background" },
];

const H = { "Authorization": `Bearer ${KEY}`, "Content-Type": "application/json" };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);
async function post(b) { const r = await fetch(BASE, { method: "POST", headers: H, body: JSON.stringify(b) }); const j = await r.json().catch(() => ({})); if (!r.ok) throw new Error(`POST ${r.status} ${JSON.stringify(j)}`); return j.result; }
async function get(id) { return (await fetch(`${BASE}/${id}`, { headers: H })).json(); }
async function waitFor(id, l) { for (let i = 0; i < 240; i++) { const t = await get(id); if (t.status === "SUCCEEDED") return t; if (["FAILED", "CANCELED"].includes(t.status)) throw new Error(`${l} ${t.status}`); if (i % 4 === 0) log(`  ${l} ${t.status} ${t.progress ?? 0}%`); await sleep(8000); } throw new Error(`${l} timeout`); }
async function dl(url, dest) { const r = await fetch(url); const b = Buffer.from(await r.arrayBuffer()); writeFileSync(dest, b); return b.length; }

async function one(a) {
  try {
    log(`[${a.name}] preview`);
    const pid = await post({ mode: "preview", prompt: a.prompt, art_style: "realistic", should_remesh: true, target_polycount: 15000 });
    const prev = await waitFor(pid, `[${a.name}] preview`);
    let fin = prev;
    try { log(`[${a.name}] refine`); const rid = await post({ mode: "refine", preview_task_id: pid, enable_pbr: true }); fin = await waitFor(rid, `[${a.name}] refine`); }
    catch (e) { log(`[${a.name}] refine skipped: ${e.message}`); }
    const glb = fin.model_urls?.glb || prev.model_urls?.glb;
    const bytes = await dl(glb, path.join(OUT, `${a.name}.glb`));
    log(`[${a.name}] DONE (${(bytes / 1e6).toFixed(2)} MB)`);
    return { name: a.name, ok: true };
  } catch (e) { log(`[${a.name}] FAILED: ${e.message}`); return { name: a.name, ok: false, error: e.message }; }
}
const res = await Promise.all(ASSETS.map(one));
log("=== plaques: " + res.filter(r => r.ok).length + "/" + res.length + " ===");
