import { useRef, useState } from 'react';
import { useZoro } from '../../zoro/useZoro';
import { CursorDot } from './CursorDot';
import { step, type Point } from './step';

/** Demo: every mouse on this screen drives its own dot. */
export function CursorsGame() {
  const area = useRef<HTMLDivElement>(null);
  const [points, setPoints] = useState<Record<number, Point>>({});
  useZoro((e) => {
    if (e.kind === 'move') setPoints((p) => step(p, e, area.current));
  });
  return (
    <div ref={area} className="game">
      {Object.entries(points).map(([n, p]) => <CursorDot key={n} n={Number(n)} x={p.x} y={p.y} />)}
    </div>
  );
}
