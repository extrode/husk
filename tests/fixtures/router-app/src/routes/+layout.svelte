<script>
  import { page } from '$app/state';
  import { beforeNavigate, goto } from '$app/navigation';
  import { browser } from '$app/environment';
  let { children } = $props();
  if (browser) window.__goto = goto;
  beforeNavigate((nav) => {
    window.__seen = (window.__seen ?? 0) + 1;
    if (window.__cancel) nav.cancel();
  });
</script>

<nav>
  <a id="to-month" href="/month">month</a>
  <a id="to-home" href="/">home</a>
  <a id="to-bad" href="/settings/nonsense">bad</a>
  <a id="to-query" href="/query">query</a>
  <a id="to-skip" href="#main">skip</a>
  <a id="to-out" href="/outside" rel="external">out</a>
</nav>
<p id="where">{page.url.pathname}{page.url.search} {browser}</p>
<p id="href">{page.url.href}</p>
<main id="main">{@render children()}</main>
