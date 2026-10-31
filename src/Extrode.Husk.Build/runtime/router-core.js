// Husk router core: route matching and load functions. No DOM and no URL class, so it runs in any
// JavaScript engine (Husk's tests run it in V8). The browser half is router.js.

export class Redirect {
  constructor(status, location) {
    this.status = status;
    this.location = location;
  }
}

export class HttpError {
  constructor(status, body) {
    this.status = status;
    this.body = typeof body === 'string' ? { message: body } : (body ?? { message: 'Error: ' + status });
  }
}

export function redirect(status, location) {
  throw new Redirect(status, String(location));
}

export function error(status, body) {
  throw new HttpError(status, body);
}

export const isRedirect = (e) => e instanceof Redirect;
export const isHttpError = (e, status) => e instanceof HttpError && (status === undefined || e.status === status);

export function routeId(route) {
  return '/' + route.pattern.map((s) => (typeof s === 'string' ? s : '[' + s.param + ']')).join('/');
}

// A trailing slash reaches the same page. Routes are in the order the build wrote them (a literal
// segment before a parameter at the same place), so the first match wins.
export function matchRoute(routes, pathname) {
  let path = pathname.split('?')[0].split('#')[0];
  while (path.length > 1 && path.endsWith('/')) path = path.slice(0, -1);
  if (!path.startsWith('/')) return null;
  const parts = path === '/' ? [] : path.slice(1).split('/');
  if (parts.some((p) => p === '')) return null;
  let decoded;
  try {
    decoded = parts.map(decodeURIComponent);
  } catch {
    return null;
  }
  for (const route of routes) {
    if (route.pattern.length !== decoded.length) continue;
    const params = {};
    let ok = true;
    for (let i = 0; i < decoded.length && ok; i++) {
      const segment = route.pattern[i];
      if (typeof segment === 'string') ok = segment === decoded[i];
      else params[segment.param] = decoded[i];
    }
    if (ok) return { route, params };
  }
  return null;
}

function failure(e) {
  if (e instanceof Redirect) return { kind: 'redirect', status: e.status, location: e.location };
  if (e instanceof HttpError) return { kind: 'error', status: e.status, message: e.body.message };
  return { kind: 'error', status: 500, message: (e && e.message) || 'Internal Error' };
}

// Imports the load modules, then runs every load (layouts outermost first, then the page) at once;
// a load's parent() waits for the loads above it. Returns { kind: 'data', data } with one entry per
// layout and the page last, or a redirect or error result, never a throw.
export async function runLoads(route, params, url, env = {}) {
  try {
    const loaders = [...route.layouts.map((l) => l.load), route.load];
    const modules = await Promise.all(loaders.map((load) => (load ? load() : null)));
    const results = [];
    const parentOf = (index) => async () => Object.assign({}, ...(await Promise.all(results.slice(0, index))));
    modules.forEach((module, index) => {
      results[index] = Promise.resolve().then(() =>
        module && module.load
          ? module.load({
              params,
              url,
              route: { id: routeId(route) },
              fetch: env.fetch,
              parent: parentOf(index),
              depends() {},
              untrack: (fn) => fn(),
              setHeaders() {},
            })
          : undefined,
      );
    });
    return { kind: 'data', data: await Promise.all(results) };
  } catch (e) {
    return failure(e);
  }
}

// What each component receives as `data`: its own result over its parents'.
export function cumulative(data) {
  const merged = [];
  let sofar = {};
  for (const d of data) {
    sofar = Object.assign({}, sofar, d);
    merged.push(sofar);
  }
  return merged;
}
