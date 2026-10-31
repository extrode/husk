# Notice

Extrode.Husk.Build, Copyright (c) 2026 Extrode (extrode.com).

Licensed under the Islamic Software License - Permissive (ISL-P), Version 1.2, WITH the Islamic
Software License - Output Exception (ISL-OE), Version 1.2. The text is in `LICENSE.md`.

SPDX-License-Identifier: LicenseRef-ISL-P-1.2 WITH AdditionRef-ISL-OE-1.2

This package redistributes the following third-party software, each under its own license. The
license texts ship in the package under `third-party/`.

| Component | Version | License | License text in the package |
|---|---|---|---|
| Svelte (compiler and runtime) | 5.57.3 | MIT | `third-party/svelte-5.57.3/svelte/LICENSE.md` |
| esm-env | as vendored with Svelte 5.57.3 | MIT | `third-party/svelte-5.57.3/esm-env/LICENSE` |
| clsx | as vendored with Svelte 5.57.3 | MIT | `third-party/svelte-5.57.3/clsx/license` |
| Microsoft ClearScript (`ClearScript.Core.dll`, `ClearScript.V8.dll`, `ClearScript.V8.ICUData.dll`, `ClearScriptV8.*` natives) | 7.5.1.1 | MIT | `third-party/clearscript-7.5.1.1/License.txt` |
| V8, in the ClearScript natives | as built into ClearScript 7.5.1.1 | BSD-3-Clause and others | `third-party/clearscript-7.5.1.1/License.txt`, `third-party/clearscript-7.5.1.1/V8/` |
| Newtonsoft.Json, a ClearScript dependency | 13.0.3 | MIT | `third-party/newtonsoft.json-13.0.3/LICENSE.md` |

The Svelte runtime files under `third-party/svelte-5.57.3/` are served by the app that uses this
package, so they reach that app's users; their license texts travel with them.
