# Same output as Node

Husk's promise is that the compiler's output is the same as Svelte compiled under Node. The Svelte compiler is pinned to one version (5.57.2), and the build's output is checked byte for byte against Node running the same bundle (law L001). A Svelte upgrade is a Husk release, never a surprise on restore.

A browser sees only the output: plain ES modules, CSS and source maps. Tested in Chrome, Firefox and Safari.
