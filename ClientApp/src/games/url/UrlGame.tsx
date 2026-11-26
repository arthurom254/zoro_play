import { useEffect, useRef } from 'react';
import { useZoro } from '../../zoro/useZoro';

function originOf(url: string): string {
  try { return new URL(url, location.href).origin; } catch { return '*'; }
}

/**
 * Hosts any web page as a game. The page receives window messages:
 *   { source: 'zoro-play', type: 'mouse', event }          every mouse event for this screen
 *   { source: 'zoro-play', type: 'state', screen, mice }   on load and whenever they change
 */
export function UrlGame({ url }: { url: string }) {
  const frame = useRef<HTMLIFrameElement>(null);
  const origin = originOf(url);
  const post = (msg: object) => frame.current?.contentWindow?.postMessage({ source: 'zoro-play', ...msg }, origin);

  const { screen, mice } = useZoro((event) => post({ type: 'mouse', event }));
  useEffect(() => { post({ type: 'state', screen, mice }); });

  return <iframe ref={frame} className="game" src={url} title="game" onLoad={() => post({ type: 'state', screen, mice })} />;
}
