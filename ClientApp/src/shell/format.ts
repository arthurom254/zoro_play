import type { MouseEvt, UpdateState } from '../zoro/types';

export function formatEvent(e: MouseEvt): string {
  const time = new Date(e.ts).toLocaleTimeString([], { hour12: false });
  const extra = e.kind === 'scroll' ? ` delta:${e.delta}` : e.kind === 'button' ? ` ${e.action}` : '';
  return `${time}  mouse${e.mouse}  ${e.label}  id:${e.id} local:${e.local}${extra}`;
}

export function bannerText(update: UpdateState, error: string | null): { text: string; bad: boolean } | null {
  if (update.phase === 'available') return { text: `Update ${update.version} available (you have ${update.current}). Press U to install.`, bad: false };
  if (update.phase === 'downloading') return { text: `Downloading update… ${update.percent}%`, bad: false };
  if (update.phase === 'error') return { text: `Update failed: ${update.message} (press U to retry)`, bad: true };
  return error ? { text: error, bad: true } : null;
}
