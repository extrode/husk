export interface Counter {
  readonly count: number;
  increment(): void;
}

export function createCounter(start: number): Counter {
  let count = $state(start);
  return {
    get count() {
      return count;
    },
    increment() {
      count += 1;
    },
  };
}

// One counter that App and Child both import, so a click in one shows in the other.
export const shared: Counter = createCounter(0);
