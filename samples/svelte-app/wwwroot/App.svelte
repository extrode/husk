<script lang="ts">
  import { fade } from 'svelte/transition';
  import Child from './Child.svelte';
  import { shared } from './counter.svelte';
  import { clicks } from './format.js';

  let items = $state([
    { id: 1, text: 'one' },
    { id: 2, text: 'two' },
  ]);
  let shown = $state(true);
  let nextId = 3;
  let total = $derived(items.length + shared.count);

  function add() {
    items.push({ id: nextId, text: `item ${nextId}` });
    nextId += 1;
  }

  function remove(id: number) {
    items = items.filter((item) => item.id !== id);
  }
</script>

<h1>Husk sample</h1>
<button id="increment" onclick={() => shared.increment()}>{clicks(shared.count)}</button>
<button id="add" onclick={add}>add</button>
<button id="toggle" onclick={() => (shown = !shown)}>toggle</button>
<p id="total">total {total}</p>
<ul>
  {#each items as item (item.id)}
    <li data-id={item.id}>{item.text} <button class="remove" onclick={() => remove(item.id)}>remove</button></li>
  {/each}
</ul>
{#if shown}
  <p id="faded" transition:fade>shown</p>
{/if}
<Child />
<img src="/logo.svg">

<style>
  h1 {
    color: rgb(255, 62, 0);
  }
</style>
