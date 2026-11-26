import type { MouseEvt } from '../../zoro/types';

export interface Point { x: number; y: number }

const clamp = (v: number, max: number) => Math.min(max, Math.max(0, v));

/** Moves the cursor of e.local by the event's delta, inside the element. */
export function step(prev: Record<number, Point>, e: MouseEvt, el: HTMLElement | null): Record<number, Point> {
  const w = el?.clientWidth ?? 800;
  const h = el?.clientHeight ?? 600;
  const cur = prev[e.local] ?? { x: w / 2, y: h / 2 };
  return { ...prev, [e.local]: { x: clamp(cur.x + (e.dx ?? 0), w), y: clamp(cur.y + (e.dy ?? 0), h) } };
}
