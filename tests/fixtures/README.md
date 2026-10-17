# Fixtures

Inputs for the compiler identity tests. This folder stands for a project: each source's `filename`
is its path below here (`wwwroot/Styled.svelte`), as the build passes it.

| File | Covers |
|---|---|
| `wwwroot/Styled.svelte` | scoped CSS, runes, an event, a child |
| `wwwroot/Plain.svelte` | no `<style>`: an empty `.svelte.css` |
| `wwwroot/clock.svelte.js` | a rune module through `compileModule` |
| `wwwroot/counter.svelte.ts` | a TypeScript rune module importing `format.ts`; stripped by Pare first |
| `wwwroot/format.ts` | the `.ts` helper; Pare's own source, not compiled by Husk |
| `wwwroot/Broken.svelte` | a syntax error |
| `wwwroot/Image.svelte` | `<img>` without `alt`: one warning |
| `wwwroot/Injected.svelte` | `css="injected"`: in dev, Svelte base64-encodes the CSS map, with `btoa` |

`*.svelte.ts.stripped.js` is Pare 0.6.0's strip of the `.svelte.ts` beside it, written by
`scripts/node-reference/strip-fixtures.cs`, committed so a Pare change shows as a diff.
`expected.tsv` is written by `scripts/node-reference/reference.mjs`; see its README.
