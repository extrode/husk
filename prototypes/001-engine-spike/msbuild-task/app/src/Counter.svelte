<script lang="ts">
  interface Props { label: string; start?: number }
  let { label, start = 0 }: Props = $props();
  let count = $state(start);
  const doubled = $derived(count * 2);
</script>

<button onclick={() => count++}>{label}: {count} (doubled {doubled})</button>

<style>
  button { font: inherit; padding: 0.5rem 1rem; }
</style>
