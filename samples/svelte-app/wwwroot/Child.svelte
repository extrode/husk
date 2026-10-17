<script lang="ts">
  import { shared } from './counter.svelte';
</script>

<p class="child">child sees {shared.count}</p>

<style>
  .child {
    color: rgb(0, 128, 0);
  }
</style>
