import { useState } from 'react';
import { useZoro } from '../zoro/useZoro';
import type { MouseSlot } from '../zoro/types';
import { EMPTY_LIVE, applyEvent } from './live';

export function MouseTile({ slot }: { slot: MouseSlot }) {
  const [live, setLive] = useState(EMPTY_LIVE);
  useZoro((e) => {
    if (e.mouse === slot.slot) setLive((l) => applyEvent(l, e));
  });

  if (!slot.connected) return <div className="tile empty">Mouse {slot.local}<br />— empty —</div>;
  return (
    <div className="tile on">
      <div className="tile-head"><b>Mouse {slot.local}</b><span>id:{slot.id}</span></div>
      <div className="tile-name" title={slot.name}>#{slot.slot} · {slot.name}</div>
      <div className="btns">
        {(['left', 'middle', 'right'] as const).map((b) => (
          <i key={b} className={live.buttons[b] ? 'down' : ''}>{b[0].toUpperCase()}</i>
        ))}
      </div>
      <div className="tile-stat">dx {live.dx} dy {live.dy}</div>
      <div className="tile-stat">scroll {live.scroll}</div>
    </div>
  );
}
