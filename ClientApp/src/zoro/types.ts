export interface MouseEvt {
  kind: 'button' | 'scroll' | 'move' | 'connect' | 'disconnect';
  mouse: number;   // global slot number
  local: number;   // mouse number on THIS screen (1..maxMice)
  screen: number;
  id: number;
  ts: number;
  label: string;   // e.g. "left-click"
  name?: string;
  button?: string; // left | right | middle | buttonN
  action?: 'down' | 'up';
  axis?: 'vertical' | 'horizontal';
  dx?: number;
  dy?: number;
  x?: number;
  y?: number;
  delta?: number;
}

export interface MouseSlot { slot: number; local: number; connected: boolean; id?: number; name?: string }

export interface ScreenInfo {
  game: number; displayId: string;
  x: number; y: number; width: number; height: number; scale: number;
  maxMice: number; firstSlot: number;
  gameUrl?: string;
}

export type UpdateState =
  | { phase: 'none' }
  | { phase: 'available'; version: string; current: string }
  | { phase: 'downloading'; percent: number }
  | { phase: 'error'; message: string };

export interface ZoroState {
  version: string;
  screen: ScreenInfo | null;
  mice: MouseSlot[];
  error: string | null;
  update: UpdateState;
}
