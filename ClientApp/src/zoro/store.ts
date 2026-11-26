import { inElectron, listen, send } from './ipc';
import type { MouseEvt, MouseSlot, ScreenInfo, ZoroState } from './types';

let state: ZoroState = { version: '', screen: null, mice: [], error: null, update: { phase: 'none' } };
const subscribers = new Set<() => void>();
const mouseSubscribers = new Set<(e: MouseEvt) => void>();
let started = false;

function set(patch: Partial<ZoroState>) {
  state = { ...state, ...patch };
  subscribers.forEach((f) => f());
}

export const getState = () => state;

export function subscribe(fn: () => void) {
  subscribers.add(fn);
  return () => { subscribers.delete(fn); };
}

export function subscribeMouse(fn: (e: MouseEvt) => void) {
  mouseSubscribers.add(fn);
  return () => { mouseSubscribers.delete(fn); };
}

export const installUpdate = () => send('install-update');
export const openDevTools = () => send('open-devtools');

const gameNumber = () => Number(location.pathname.match(/game(\d+)/)?.[1] ?? 1);

/** Registers every backend channel once, then tells C# this page is ready. */
export function startZoro() {
  if (started) return;
  started = true;

  listen<{ version: string }>('app-info', (d) => set({ version: d.version }));
  listen<ScreenInfo>('screen-info', (screen) => set({ screen }));
  listen<MouseSlot[]>('mouse-snapshot', (mice) => set({ mice }));
  listen<{ message: string }>('driver-error', (d) => set({ error: d.message }));
  listen<{ version: string; current: string }>('update-available', (d) =>
    set({ update: { phase: 'available', version: d.version, current: d.current } }));
  listen<{ percent: number }>('update-progress', (d) => set({ update: { phase: 'downloading', percent: d.percent } }));
  listen<{ message: string }>('update-error', (d) => set({ update: { phase: 'error', message: d.message } }));
  listen<MouseEvt>('mouse-event', (e) => mouseSubscribers.forEach((f) => f(e)));

  if (inElectron) send('ui-ready', gameNumber());
  else // plain browser (npm run dev without Electron): fake a screen so the UI is visible
    set({
      screen: { game: gameNumber(), displayId: 'browser', x: 0, y: 0, width: innerWidth, height: innerHeight, scale: 1, maxMice: 3, firstSlot: 1 },
      mice: [1, 2, 3].map((n) => ({ slot: n, local: n, connected: false })),
    });
}
