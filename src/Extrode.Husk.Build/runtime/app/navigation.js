// $app/navigation. The router (router.js) attaches itself when it starts.
import { onDestroy, onMount } from 'svelte';

const before = new Set();
const after = new Set();
let router = null;
let last = null;
let committing = false;

export function _attach(attached) {
  router = attached;
}

export function _before() {
  return [...before];
}

export function _committing() {
  committing = true;
}

export function _after(navigation) {
  committing = false;
  last = navigation;
  for (const callback of [...after]) callback(navigation);
}

export function goto(url, options = {}) {
  return router.goto(url, options);
}

export function beforeNavigate(callback) {
  before.add(callback);
  onDestroy(() => before.delete(callback));
}

export function afterNavigate(callback) {
  after.add(callback);
  onMount(() => {
    // A component the navigation itself mounts gets the real call from _after, not a replay of the previous one.
    if (last && !committing) callback({ ...last, type: 'enter', from: null });
  });
  onDestroy(() => after.delete(callback));
}

export function preloadCode(...pathnames) {
  return router.preload(...pathnames);
}
