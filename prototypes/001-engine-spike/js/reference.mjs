// Reference run: the same compiler bundle under Node. Writes one line per file:
// path \t ok|err \t sha256 of js.code or the error code. Prints load and compile timings.
// Usage: node reference.mjs <list.txt> <out.tsv> [passes]
import { readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";

const [list, out, passesArg] = process.argv.slice(2);
const passes = Number(passesArg ?? 3);
const files = readFileSync(list, "utf8").split("\n").filter(Boolean);
const sources = files.map((f) => readFileSync(f, "utf8"));

let t = performance.now();
const { compile } = createRequire(import.meta.url)("./node_modules/svelte/compiler/index.js");
console.log(`load ${(performance.now() - t).toFixed(1)} ms`);

function compileOne(source, filename) {
  try {
    return ["ok", compile(source, { filename, generate: "client" }).js.code];
  } catch (e) {
    return ["err", e.code ?? String(e.message)];
  }
}

const lines = [];
t = performance.now();
for (let i = 0; i < files.length; i++) {
  const [kind, value] = compileOne(sources[i], files[i]);
  lines.push(`${files[i]}\t${kind}\t${kind === "ok" ? createHash("sha256").update(value).digest("hex") : value}`);
}
console.log(`first pass ${(performance.now() - t).toFixed(0)} ms`);
writeFileSync(out, lines.join("\n") + "\n");

const times = [];
for (let p = 0; p < passes; p++) {
  t = performance.now();
  for (let i = 0; i < files.length; i++) compileOne(sources[i], files[i]);
  times.push(performance.now() - t);
}
times.sort((a, b) => a - b);
console.log(`warm passes best ${times[0].toFixed(0)} ms, median ${times[times.length >> 1].toFixed(0)} ms`);
console.log(`ok ${lines.filter((l) => l.includes("\tok\t")).length}, err ${lines.filter((l) => l.includes("\terr\t")).length}`);
