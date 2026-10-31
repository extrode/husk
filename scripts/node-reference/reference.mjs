// The reference run: Husk's fixtures through the vendored Svelte compiler under Node, with the
// same glue Husk's engines run (src/Extrode.Husk.Compiler/js/husk-compile.js). Writes
// tests/fixtures/expected.tsv. Run by hand from the repo root when the pin or a fixture changes:
//   node scripts/node-reference/reference.mjs
// Over a corpus (scripts/node-reference/corpus.sh), every source below <root> into <out.tsv>, the
// stripped .svelte.ts read from <stripped>:
//   node scripts/node-reference/reference.mjs <root> <out.tsv> <stripped>
import { readFileSync, readdirSync, writeFileSync, existsSync } from "node:fs";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { join } from "node:path";
import vm from "node:vm";

const [root, out, strippedRoot] = process.argv.length > 2
  ? process.argv.slice(2)
  : ["tests/fixtures", "tests/fixtures/expected.tsv", "tests/fixtures"];
const skipped = new Set(["node_modules", ".git", ".svelte-kit", "build", "dist"]);
const svelte = createRequire(import.meta.url)("../../third-party/svelte-5.57.3/svelte/compiler/index.js");
vm.runInThisContext(readFileSync("src/Extrode.Husk.Compiler/js/husk-compile.js", "utf8"));

const sha = (text) => createHash("sha256").update(text, "utf8").digest("hex");
const diagnostic = (d) => `${d.code}@${d.line}:${d.column}`;

const walk = (dir) => readdirSync(join(root, dir), { withFileTypes: true }).flatMap((e) => {
  const path = dir ? `${dir}/${e.name}` : e.name;
  if (e.isDirectory()) return skipped.has(e.name) ? [] : walk(path);
  return e.isFile() && /\.svelte(\.ts|\.js)?$/.test(e.name) ? [path] : [];
});
const sources = walk("").sort();

const lines = ["# filename\tdev\tresult\tjs\tcss\tmap\tdiagnostics"];
for (const filename of sources) {
  const path = join(root, filename);
  const isTs = filename.endsWith(".svelte.ts");
  const kind = filename.endsWith(".svelte") ? "component" : "module";
  const original = readFileSync(path, "utf8");
  const strippedPath = join(strippedRoot, filename) + ".stripped.js";
  if (isTs && !existsSync(strippedPath)) {
    for (const dev of [false, true]) lines.push([filename, dev, "err", "-", "-", "-", "strip"].join("\t"));
    continue;
  }
  const source = isTs ? readFileSync(strippedPath, "utf8") : original;
  for (const dev of [false, true]) {
    const r = JSON.parse(globalThis.huskCompile(svelte, kind, source, filename, dev, isTs ? original : null));
    lines.push(r.error
      ? [filename, dev, "err", "-", "-", "-", diagnostic(r.error)].join("\t")
      : [filename, dev, "ok", sha(r.js), sha(r.css), sha(r.map), r.warnings.map(diagnostic).join(",") || "-"].join("\t"));
  }
}
writeFileSync(out, lines.join("\n") + "\n");
console.log(`wrote ${lines.length - 1} rows for ${sources.length} sources to ${out}`);
