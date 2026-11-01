// $app/environment. `dev` is set by start.js from the build (true in a Development build).
export const browser = true;
export let dev = false;
export const building = false;
export const version = '';

export function _setDev(value) {
  dev = !!value;
}
