# Notice

Extrode.Husk.Build, Copyright (c) 2026 Extrode (extrode.com).

This package redistributes the following third-party software, each under its own license. The
license texts ship in the package under `third-party/`.

| Component | Version | License | License text in the package |
|---|---|---|---|
| Svelte (compiler and runtime) | 5.57.2 | MIT | `third-party/svelte-5.57.2/svelte/LICENSE.md` |
| esm-env | as vendored with Svelte 5.57.2 | MIT | `third-party/svelte-5.57.2/esm-env/LICENSE` |
| clsx | as vendored with Svelte 5.57.2 | MIT | `third-party/svelte-5.57.2/clsx/license` |
| Microsoft ClearScript (`ClearScript.Core.dll`, `ClearScript.V8.dll`, `ClearScript.V8.ICUData.dll`, `ClearScriptV8.*` natives) | 7.5.1.1 | MIT | `third-party/clearscript-7.5.1.1/License.txt` |
| V8, in the ClearScript natives | as built into ClearScript 7.5.1.1 | BSD-3-Clause and others | `third-party/clearscript-7.5.1.1/License.txt`, `third-party/clearscript-7.5.1.1/V8/` |
| Newtonsoft.Json, a ClearScript dependency | 13.0.3 | MIT | `third-party/newtonsoft.json-13.0.3/LICENSE.md` |

The Svelte runtime files under `third-party/svelte-5.57.2/` are served by the app that uses this
package, so they reach that app's users; their license texts travel with them.
