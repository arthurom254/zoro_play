import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { tanstackRouter } from '@tanstack/router-plugin/vite';

// The router plugin must come before the React plugin. It generates src/routeTree.gen.ts.
export default defineConfig({
  base: '/',
  plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react()],
  build: { outDir: '../wwwroot', emptyOutDir: true },
  server: { port: 5173, strictPort: true },
});
