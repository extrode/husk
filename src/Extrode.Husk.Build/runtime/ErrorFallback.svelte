<script>
  import { page } from './app/state.svelte.js';
</script>

<h1>{page.status}</h1>
<p>{page.error?.message}</p>
