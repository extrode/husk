<script>
  import Self from './Root.svelte';
  import { view } from './app/state.svelte.js';

  let { index = 0 } = $props();
  const item = $derived(view.chain[index]);
  const Component = $derived(item?.component);
  const last = $derived(index >= view.chain.length - 1);
</script>

{#if item}
  {#if last}
    <Component {...item.props} />
  {:else}
    <Component {...item.props}><Self index={index + 1} /></Component>
  {/if}
{/if}
