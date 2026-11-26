import { createFileRoute } from '@tanstack/react-router';
import { GameShell } from '../shell/GameShell';
import { CursorsGame } from '../games/cursors/CursorsGame';

export const Route = createFileRoute('/game1')({ component: Game1 });

function Game1() {
  return (
    <GameShell>
      <CursorsGame />
    </GameShell>
  );
}
