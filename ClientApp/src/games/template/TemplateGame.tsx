import { useZoro } from '../../zoro/useZoro';
import type { MouseEvt } from '../../zoro/types';

/**
 * GAME TEMPLATE - copy this folder, rename it, then point a route at it (see README).
 * You only render inside the stage; the monitor label and the mice bar come from <GameShell>.
 */
export function TemplateGame() {
  const { screen, mice } = useZoro(onMouse);

  // e.local = mouse number on THIS screen (1..maxMice) - use it to tell players apart.
  // e.kind = 'button' | 'scroll' | 'move' | 'connect' | 'disconnect'
  function onMouse(e: MouseEvt) {
    if (e.kind === 'button' && e.action === 'down') console.log(`player ${e.local} pressed ${e.button}`);
  }

  return (
    <div className="game template">
      <div>
        <h2>Game for monitor {screen?.game}</h2>
        <p>{mice.filter((m) => m.connected).length} of {screen?.maxMice ?? 0} mice connected. Replace this component.</p>
      </div>
    </div>
  );
}
