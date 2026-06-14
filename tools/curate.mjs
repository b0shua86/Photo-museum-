// In-place curation of the museum content JSON:
//  1) add a "Contemporary" hall (2000–present) and move recent artists into it
//  2) flag artworks that depict nudity as `mature` (hidden on the wall until the
//     visitor opts in)
import { readFileSync, writeFileSync } from "node:fs";

const DIR = "/Users/miller/Desktop/camera-obscura/Assets/StreamingAssets/content";
const movements = JSON.parse(readFileSync(`${DIR}/movements.json`, "utf8"));
const artists = JSON.parse(readFileSync(`${DIR}/artists.json`, "utf8"));

// 1) New hall.
if (!movements.find((m) => m.id === "contemporary")) {
  movements.push({
    id: "contemporary",
    name: "Contemporary & Digital",
    period: "c. 2000–present",
    startYear: 2000,
    endYear: null,
    blurb:
      "The medium in the new century — large-format tableaux, the deadpan digital sublime, staged psychological fictions and the self-portrait reborn on the social web. Photography becomes vast, constructed and screen-native.",
    color: "#3f7d8c",
    music: { youtubeId: "", title: "Contemporary · Ambient" },
  });
}

const CONTEMP = new Set([
  "andreas-gursky", "edward-burtynsky", "naoya-hatakeyama", "thomas-ruff", "thomas-struth",
  "crewdson", "erwin-olaf", "kyle-thompson", "philip-lorca-dicorcia", "roger-ballen",
]);
let moved = 0;
for (const a of artists) if (CONTEMP.has(a.id)) { a.movementId = "contemporary"; moved++; }

// 2) Mature (nudity) flag — keyword heuristic over title + description, plus a
//    few artists whose flagged series are predominantly figure/nude studies.
const NUDE_RE = /\b(nude|nudes|naked|nu|akt|desnudo|undressed|bare\s+(body|skin)|torso)\b/i;
const NUDE_ARTISTS = new Set(["edward-weston", "hans-bellmer", "anne-brigman", "ruth-bernhard", "bill-brandt", "lucien-clergue"]);
let flagged = 0, total = 0;
for (const a of artists) {
  for (const w of a.artworks) {
    total++;
    const text = `${w.title} ${w.description || ""}`;
    const mature = NUDE_RE.test(text) || (NUDE_ARTISTS.has(a.id) && /nude|figure|body|torso|female|male/i.test(text));
    w.mature = !!mature;
    if (mature) flagged++;
  }
}

writeFileSync(`${DIR}/movements.json`, JSON.stringify(movements, null, 2));
writeFileSync(`${DIR}/artists.json`, JSON.stringify(artists, null, 2));
console.log(`Added 'contemporary' hall, moved ${moved} artists into it.`);
console.log(`Flagged ${flagged}/${total} artworks as mature (nudity).`);
console.log(`Movements now: ${movements.filter(m => artists.some(a => a.movementId === m.id)).map(m => m.id).join(", ")}`);
