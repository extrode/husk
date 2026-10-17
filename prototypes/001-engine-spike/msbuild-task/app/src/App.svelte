<script lang="ts">
  import Counter from "./Counter.js";
  let items: string[] = $state(["one", "two"]);
</script>

<h1>Svelte via ClearScript</h1>
<Counter label="Clicks" start={1} />
<ul>
  {#each items as item (item)}<li>{item}</li>{/each}
</ul>
<button id="add" onclick={() => items.push(`item ${items.length + 1}`)}>Add</button>
