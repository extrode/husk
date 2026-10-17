// Husk's one call into the Svelte compiler. The same file runs in Husk's V8 engines and under
// Node in scripts/node-reference/reference.mjs, so both pass the compiler the same options.
// Returns JSON: { js, css, map, warnings, error }, each diagnostic { code, message, line, column }
// with Svelte's 1-based line and 0-based column.
globalThis.huskCompile = function (svelte, kind, source, filename, dev, sourcesContent) {
  const position = (d) => ({
    code: d.code ?? "unknown",
    message: String(d.message ?? d),
    line: d.start?.line ?? 0,
    column: d.start?.column ?? 0,
  });
  try {
    const result = kind === "module"
      ? svelte.compileModule(source, { filename, dev, generate: "client" })
      : svelte.compile(source, { filename, dev, generate: "client", css: "external" });
    const map = JSON.parse(JSON.stringify(result.js.map));
    // A .svelte.ts is compiled from Pare's strip; devtools should show the TypeScript.
    if (sourcesContent != null)
      map.sourcesContent = [sourcesContent];
    return JSON.stringify({
      js: result.js.code,
      css: result.css?.code ?? "",
      map: JSON.stringify(map),
      warnings: result.warnings.map(position),
      error: null,
    });
  } catch (e) {
    return JSON.stringify({ js: null, css: null, map: null, warnings: [], error: position(e) });
  }
};
