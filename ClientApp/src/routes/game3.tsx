import { createFileRoute } from '@tanstack/react-router';
import { GameShell } from '../shell/GameShell';
import { TemplateGame } from '../games/template/TemplateGame';

export const Route = createFileRoute('/game3')({ component: Game3 });

function Game3() {
  return (
    <GameShell>
      <TemplateGame />
    </GameShell>
  );
}
