# saaaat references

`<saaaat-sha>/<page>.<locale>.<browser>.txt`: the visible text of one page of the saaaat Vite build, after it
settles, with the clock and stored state pinned (`SaaaatParityTests.Pinned`). `index` is `/`; other pages join
their path with `-`. Per browser because ICU and `innerText` differ between Blink, Gecko and WebKit.

Record (needs a Vite build, see `scripts/node-reference/saaaat-vite-build.sh`):
`HUSK_SAAAAT_RECORD=1 HUSK_SAAAAT_VITE_BUILD=<build folder> dotnet test tests/Extrode.Husk.Integration --filter "FullyQualifiedName~Recording_writes"`.
The test refuses a saaaat checkout whose commit has no folder here.
