# Node reference

The one place Node runs: by hand, when the Svelte pin or a fixture changes. Never in a build or a
test. Both scripts run from the repo root, and neither needs `npm install`: `reference.mjs` loads
the vendored compiler (`third-party/svelte-5.57.2/svelte/compiler/index.js`) and the same glue
Husk's engines run (`src/Extrode.Husk.Compiler/js/husk-compile.js`).

```sh
dotnet run scripts/node-reference/strip-fixtures.cs   # .svelte.ts -> .svelte.ts.stripped.js (Pare 0.6.0)
node scripts/node-reference/reference.mjs              # -> tests/fixtures/expected.tsv
```

`expected.tsv` has one row per fixture and `dev` value: the result (`ok` or `err`), SHA-256 of
`js.code`, of `css.code` (`""` when there is no `<style>`), of the source map text Husk writes,
and the diagnostics as `code@line:column` with Svelte's 1-based line and 0-based column. A
`.svelte.ts` row compiles the stripped text and hashes the map after its `sourcesContent` is set
to the original TypeScript, as Husk writes it.

Measured with Node 24.16.0. The rows depend on the compiler bundle and the glue, not on Node's
version.
