<script>
  import { page } from '$app/state';
  import { goto } from '$app/navigation';
</script>

<h1 id="title">query</h1>
<button id="set" onclick={() => goto('?year=1447', { replaceState: true })}>set</button>
<p id="year">{page.url.searchParams.get('year') ?? 'none'}</p>
