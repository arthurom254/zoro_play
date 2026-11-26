import { useZoro } from '../zoro/useZoro';

export function MonitorLabel() {
  const { screen, version } = useZoro();
  if (!screen) return <div className="overlay monitor"><h1>Monitor</h1></div>;
  const mice = screen.maxMice ? `mice #${screen.firstSlot}–#${screen.firstSlot + screen.maxMice - 1}` : 'no mice assigned';
  return (
    <div className="overlay monitor">
      <h1>Monitor {screen.game}</h1>
      <p>/game{screen.game} · display {screen.displayId} · {screen.width}×{screen.height} @ {screen.x},{screen.y} · {mice}</p>
      <small>Zoro Play v{version} · H cursor · L log · U update · F12 devtools</small>
    </div>
  );
}
