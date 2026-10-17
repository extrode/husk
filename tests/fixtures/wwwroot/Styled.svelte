<script>
  import Plain from './Plain.svelte';

  let { step = 1 } = $props();
  let count = $state(0);
  let doubled = $derived(count * 2);
</script>

<button class="counter" onclick={() => (count += step)}>
  {count} doubled is {doubled}
</button>
<Plain label="child" />

<style>
  .counter {
    color: rebeccapurple;
    font-weight: bold;
  }
</style>
