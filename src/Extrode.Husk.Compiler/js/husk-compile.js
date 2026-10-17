// Husk's one call into the Svelte compiler. The same file runs in Husk's V8 engines and under
// Node in scripts/node-reference/reference.mjs, so both pass the compiler the same options.
// Returns JSON: { js, css, map, warnings, error }, each diagnostic { code, message, line, column }
// with Svelte's 1-based line and 0-based column.
// V8 under ClearScript has no btoa or atob, and Svelte picks its source map encoder when it loads:
// window.btoa, else Buffer, else a compile of injected CSS in dev fails. So Husk runs this file
// before the compiler, and defines them as a browser does. Node has btoa, so the reference run
// skips this.
if (typeof globalThis.btoa !== "function") {
  const digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  globalThis.btoa = (text) => {
    const s = String(text);
    let out = "";
    for (let i = 0; i < s.length; i += 3) {
      const a = s.charCodeAt(i), b = s.charCodeAt(i + 1), c = s.charCodeAt(i + 2);
      if (a > 255 || b > 255 || c > 255)
        throw new Error("btoa: the string has a character outside Latin-1");
      const n = (a << 16) | ((b || 0) << 8) | (c || 0);
      out += digits[n >> 18] + digits[(n >> 12) & 63]
        + (i + 1 < s.length ? digits[(n >> 6) & 63] : "=") + (i + 2 < s.length ? digits[n & 63] : "=");
    }
    return out;
  };
  globalThis.atob = (text) => {
    const s = String(text).replace(/[\t\n\f\r ]/g, "").replace(/={1,2}$/, "");
    let out = "", bits = 0, n = 0;
    for (const ch of s) {
      const v = digits.indexOf(ch);
      if (v < 0)
        throw new Error("atob: the string is not base64");
      n = ((n << 6) | v) & 0xffffff;
      bits += 6;
      if (bits >= 8) {
        bits -= 8;
        out += String.fromCharCode((n >> bits) & 255);
      }
    }
    return out;
  };
  globalThis.window ??= globalThis;
}

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
