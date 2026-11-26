import type { MouseEvt } from '../zoro/types';

export interface Live { buttons: Record<string, boolean>; dx: number; dy: number; scroll: number }

export const EMPTY_LIVE: Live = { buttons: {}, dx: 0, dy: 0, scroll: 0 };

export function applyEvent(live: Live, e: MouseEvt): Live {
  if (e.kind === 'connect') return EMPTY_LIVE;
  if (e.kind === 'button' && e.button) return { ...live, buttons: { ...live.buttons, [e.button]: e.action === 'down' } };
  if (e.kind === 'move') return { ...live, dx: e.dx ?? 0, dy: e.dy ?? 0 };
  if (e.kind === 'scroll') return { ...live, scroll: e.delta ?? 0 };
  return live;
}
