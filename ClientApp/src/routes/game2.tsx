import { createFileRoute } from '@tanstack/react-router';
import { GameShell } from '../shell/GameShell';
import { ZoraGame } from '../games/zora/ZoraGame';

export const Route = createFileRoute('/game2')({ component: Game2 });

function Game2() {
  return (
    <GameShell>
      <ZoraGame />
    </GameShell>
  );
}
