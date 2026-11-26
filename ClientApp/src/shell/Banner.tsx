import { useZoro } from '../zoro/useZoro';
import { bannerText } from './format';
import { useKey } from './useKey';

export function Banner() {
  const { update, error, installUpdate } = useZoro();
  useKey('u', () => {
    if (update.phase === 'available' || update.phase === 'error') installUpdate();
  });
  const banner = bannerText(update, error);
  if (!banner) return null;
  return <div className={`overlay banner${banner.bad ? ' bad' : ''}`}>{banner.text}</div>;
}
