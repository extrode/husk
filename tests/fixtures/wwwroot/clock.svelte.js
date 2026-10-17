export function clock(start) {
  let now = $state(start);
  return {
    get now() {
      return now;
    },
    tick() {
      now += 1;
    },
  };
}
