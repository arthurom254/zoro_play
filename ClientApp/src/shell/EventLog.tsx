import { useState } from 'react';
import { useZoro } from '../zoro/useZoro';
import type { MouseEvt } from '../zoro/types';
import { formatEvent } from './format';
import { useKey } from './useKey';

export function EventLog() {
  const [open, setOpen] = useState(false);
  const [rows, setRows] = useState<MouseEvt[]>([]);
  useKey('l', () => setOpen((o) => !o));
  useZoro((e) => {
    if (e.kind !== 'move') setRows((r) => [...r.slice(-59), e]);
  });
  if (!open) return null;
  return (
    <div className="overlay log">
      {rows.map((e, i) => <div key={`${e.ts}-${i}`}>{formatEvent(e)}</div>)}
    </div>
  );
}
