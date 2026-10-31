// Husk router, browser half: history, link clicks, navigation. Matching and loads are router-core.js.
import { mount, tick } from 'svelte';
import Root from './Root.svelte';
import ErrorFallback from './ErrorFallback.svelte';
import { matchRoute, runLoads, cumulative, routeId } from './router-core.js';
import { _attach, _before, _after } from './app/navigation.js';
import { page, view } from './app/state.svelte.js';
import { _setDev } from './app/environment.js';

const MAX_REDIRECTS = 20;

let config;
let currentUrl = null;
let token = 0;
let index = 0;
let ignorePop = false;

const web = (url) => url.protocol === 'http:' || url.protocol === 'https:';
const internal = (url) => url.origin === location.origin;

export async function start({ routes, errorPage, rootLayout, dev, target }) {
  config = { routes, errorPage, rootLayout };
  _setDev(dev);
  _attach({ goto, preload });
  mount(Root, { target });
  addEventListener('popstate', onPop);
  addEventListener('hashchange', onHash);
  document.addEventListener('click', onClick);
  history.replaceState({ idx: 0 }, '', location.href);
  await navigate(location.href, { initial: true, replace: true, type: 'enter' });
}

export function goto(href, options = {}) {
  return navigate(String(href), {
    replace: !!options.replaceState,
    keepFocus: !!options.keepFocus,
    noScroll: !!options.noScroll,
    type: 'goto',
  });
}

async function importRoute(route) {
  const [page_, ...layouts] = await Promise.all([route.page(), ...route.layouts.map((l) => l.component())]);
  return { page: page_.default, layouts: layouts.map((m) => m.default) };
}

export async function preload(...pathnames) {
  for (const path of pathnames) {
    const matched = matchRoute(config.routes, new URL(path, location.href).pathname);
    if (!matched) throw new Error('Unable to find route for ' + path);
    await Promise.all([importRoute(matched.route), matched.route.load?.(), ...matched.route.layouts.map((l) => l.load?.())]);
  }
}

const describe = (url, matched) =>
  url && { url, params: matched ? matched.params : page.params, route: { id: matched ? routeId(matched.route) : page.route.id } };

async function errorView(status, message) {
  const component = config.errorPage ? (await config.errorPage()).default : ErrorFallback;
  const chain = [];
  const root = config.rootLayout;
  if (root) {
    const loaded = await runLoads({ pattern: [], layouts: [], load: root.load }, {}, currentUrl);
    if (loaded.kind === 'data') chain.push({ component: (await root.component()).default, props: { data: loaded.data[0] ?? {}, params: {} } });
  }
  chain.push({ component, props: {} });
  return { chain, params: {}, status, error: { message }, route: { id: null }, data: {} };
}

async function navigate(href, options, redirects = 0) {
  const url = new URL(href, currentUrl ?? location.href);
  if (!web(url)) {
    console.warn('Husk: not navigating to', url.protocol);
    return false;
  }
  const external = !internal(url);
  const matched = external ? null : matchRoute(config.routes, url.pathname);

  if (!options.initial && !options.redirected) {
    let cancelled = false;
    const navigation = {
      type: options.type,
      from: describe(currentUrl, null),
      to: { url, params: matched ? matched.params : null, route: { id: matched ? routeId(matched.route) : null } },
      willUnload: external,
      delta: options.delta,
      cancel: () => {
        cancelled = true;
      },
    };
    for (const callback of _before()) callback(navigation);
    if (cancelled) {
      if (options.popped && options.delta) {
        ignorePop = true;
        history.go(-options.delta);
      }
      return false;
    }
  }
  if (external) {
    location.assign(url.href);
    return true;
  }

  const mine = ++token;
  let next;
  if (!matched) next = await errorView(404, 'Not Found');
  else {
    const [loaded, imported] = await Promise.all([
      runLoads(matched.route, matched.params, url, { fetch: window.fetch.bind(window) }),
      importRoute(matched.route).catch((e) => e),
    ]);
    if (mine !== token) return false;
    if (loaded.kind === 'redirect') {
      if (redirects >= MAX_REDIRECTS) next = await errorView(500, 'Redirect loop');
      else return navigate(new URL(loaded.location, url).href, { ...options, redirected: true }, redirects + 1);
    } else if (imported instanceof Error) {
      console.error(imported);
      next = await errorView(500, imported.message);
    } else if (loaded.kind === 'error') next = await errorView(loaded.status, loaded.message);
    else {
      const merged = cumulative(loaded.data);
      const chain = [...imported.layouts, imported.page].map((component, i) => ({
        component,
        props: { data: merged[i], params: matched.params },
      }));
      next = { chain, params: matched.params, status: 200, error: null, route: { id: routeId(matched.route) }, data: merged[merged.length - 1] };
    }
  }
  if (mine !== token) return false;

  if (options.popped) {
    index = history.state?.idx ?? index;
    if (url.href !== location.href) history.replaceState({ idx: index }, '', url.href);
  } else if (options.replace) history.replaceState({ idx: index }, '', url.href);
  else history.pushState({ idx: ++index }, '', url.href);

  const from = describe(currentUrl, null);
  currentUrl = url;
  page.url = url;
  page.params = next.params;
  page.route = next.route;
  page.status = next.status;
  page.error = next.error;
  page.data = next.data;
  view.chain = next.chain;
  await tick();

  if (!options.popped && !options.noScroll) {
    const target = url.hash && document.getElementById(decodeURIComponent(url.hash.slice(1)));
    if (target) target.scrollIntoView();
    else scrollTo(0, 0);
  }
  if (!options.keepFocus && document.activeElement && document.activeElement !== document.body) document.activeElement.blur();

  _after({ type: options.type, from, to: describe(url, matched), willUnload: false, delta: options.delta });
  return true;
}

function onPop(event) {
  if (ignorePop) {
    ignorePop = false;
    return;
  }
  const url = new URL(location.href);
  const idx = event.state?.idx ?? index;
  if (currentUrl && url.pathname === currentUrl.pathname && url.search === currentUrl.search) {
    index = idx;
    currentUrl = url;
    page.url = url;
    return;
  }
  navigate(url.href, { popped: true, type: 'popstate', delta: idx - index });
}

function onHash() {
  const url = new URL(location.href);
  if (history.state?.idx === undefined) history.replaceState({ idx: ++index }, '', url.href);
  currentUrl = url;
  page.url = url;
}

function onClick(event) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.defaultPrevented) return;
  const anchor = event.composedPath().find((n) => n instanceof HTMLAnchorElement);
  if (!anchor || !anchor.hasAttribute('href') || anchor.hasAttribute('download')) return;
  if (anchor.target && anchor.target !== '_self') return;
  if ((anchor.getAttribute('rel') ?? '').split(/\s+/).includes('external')) return;
  const url = new URL(anchor.href);
  if (!web(url) || !internal(url)) return;
  if (url.hash && url.pathname === location.pathname && url.search === location.search) return;
  event.preventDefault();
  navigate(url.href, { replace: url.href === location.href, type: 'link' });
}
