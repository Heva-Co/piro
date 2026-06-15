import { MediaQuery } from "svelte/reactivity";

export function useIsMobile(breakpoint = 768) {
  const mq = new MediaQuery(`(max-width: ${breakpoint - 1}px)`);
  return { get current() { return mq.current; } };
}

export class IsMobile {
  #mq: MediaQuery;
  constructor(breakpoint = 768) {
    this.#mq = new MediaQuery(`(max-width: ${breakpoint - 1}px)`);
  }
  get current() { return this.#mq.current; }
}
