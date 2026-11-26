import { useEffect, type ReactNode } from 'react';
import { useZoro } from '../zoro/useZoro';
import { UrlGame } from '../games/url/UrlGame';
import { Banner } from './Banner';
import { EventLog } from './EventLog';
import { MiceBar } from './MiceBar';
import { MonitorLabel } from './MonitorLabel';
import { useKey } from './useKey';

/** Shared layout. Your game goes in `children` and gets the whole stage area. */
export function GameShell({ children }: { children: ReactNode }) {
  const { screen, openDevTools } = useZoro();
  useEffect(() => { document.body.classList.add('nocursor'); }, []);
  useKey('h', () => document.body.classList.toggle('nocursor'));
  useKey('F12', openDevTools);

  return (
    <div className="shell">
      <main className="stage">
        {screen?.gameUrl ? <UrlGame url={screen.gameUrl} /> : children}
        <MonitorLabel />
        <Banner />
        <EventLog />
      </main>
      <MiceBar />
    </div>
  );
}
