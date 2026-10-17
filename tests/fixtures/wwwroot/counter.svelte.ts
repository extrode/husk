import { format } from './format.ts';

interface Counter {
  readonly label: string;
  increment(by?: number): void;
}

export function counter(start: number): Counter {
  let count: number = $state(start);
  return {
    get label(): string {
      return format(count);
    },
    increment(by: number = 1): void {
      count += by;
    },
  };
}
