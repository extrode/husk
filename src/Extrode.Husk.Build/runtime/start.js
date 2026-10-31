// The entry module: runs the app's client init hook, then starts the router. The root layout is
// imported by the router, after init, because the app's stores read saved data when it loads.
import { hooks, errorPage, rootLayout, routes, dev, target } from './routes.js';
import { start } from './router.js';

if (hooks) {
  const module = await hooks();
  if (module.init) await module.init();
}
await start({ routes, errorPage, rootLayout, dev, target: document.querySelector(target) });
