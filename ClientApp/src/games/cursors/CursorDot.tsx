import { COLORS } from './palette';

export function CursorDot({ n, x, y }: { n: number; x: number; y: number }) {
  return <div className="dot" style={{ left: x, top: y, background: COLORS[(n - 1) % COLORS.length] }}>{n}</div>;
}
