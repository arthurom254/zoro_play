import { useEffect, useRef, useSyncExternalStore } from 'react';
import { getState, installUpdate, openDevTools, subscribe, subscribeMouse } from './store';
import type { MouseEvt } from './types';

/**
 * The one hook for all backend communication.
 *   const { screen, mice, error, update, version } = useZoro(onMouse);
 * onMouse (optional) is called for every mouse event routed to this screen.
 */
export function useZoro(onMouse?: (e: MouseEvt) => void) {
  const state = useSyncExternalStore(subscribe, getState);
  const handler = useRef(onMouse);
  handler.current = onMouse;
  useEffect(() => subscribeMouse((e) => handler.current?.(e)), []);
  return { ...state, installUpdate, openDevTools };
}
