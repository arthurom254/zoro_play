type Raw = (event: unknown, payload: unknown) => void;
interface Renderer {
  on(channel: string, fn: Raw): void;
  removeListener(channel: string, fn: Raw): void;
  send(channel: string, ...args: unknown[]): void;
}

// nodeIntegration is on, so window.require exists inside Electron (bundlers must not touch it)
function getRenderer(): Renderer | null {
  try {
    const req = (window as unknown as { require?: (m: string) => { ipcRenderer: Renderer } }).require;
    return req ? req('electron').ipcRenderer : null;
  } catch {
    return null;
  }
}

const renderer = getRenderer();
export const inElectron = renderer !== null;

// Electron.NET delivers payloads as "json" or [ "json" ]
function unwrap(a: unknown): unknown {
  let v = a;
  if (Array.isArray(v) && v.length === 1 && typeof v[0] === 'string') v = v[0];
  while (typeof v === 'string') {
    try { v = JSON.parse(v); } catch { break; }
  }
  return v;
}

export function listen<T>(channel: string, fn: (data: T) => void): () => void {
  if (!renderer) return () => {};
  const handler: Raw = (_event, payload) => fn(unwrap(payload) as T);
  renderer.on(channel, handler);
  return () => renderer.removeListener(channel, handler);
}

export function send(channel: string, ...args: unknown[]): void {
  renderer?.send(channel, ...args);
}
