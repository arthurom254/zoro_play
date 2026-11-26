import { useEffect, useRef } from 'react';

export function useKey(key: string, fn: () => void) {
  const latest = useRef(fn);
  latest.current = fn;
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key.toLowerCase() === key.toLowerCase()) latest.current();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [key]);
}
