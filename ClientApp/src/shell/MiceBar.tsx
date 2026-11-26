import { useZoro } from '../zoro/useZoro';
import { MouseTile } from './MouseTile';

export function MiceBar() {
  const { mice } = useZoro();
  return (
    <footer className="mice">
      {mice.length
        ? mice.map((m) => <MouseTile key={m.slot} slot={m} />)
        : <span className="mice-none">No mice assigned to this screen. Connect them and restart to regenerate the configuration.</span>}
    </footer>
  );
}
