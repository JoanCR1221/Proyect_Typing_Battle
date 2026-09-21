import { defineConfig } from 'vitest/config';

export default defineConfig({
  define: {
    // Lo mismo que hace DefinePlugin en Webpack.
    __API_URL__: JSON.stringify('http://localhost:5080'),
  },
  test: {
    environment: 'jsdom',
    include: ['test/**/*.spec.ts'],
    setupFiles: ['test/setup.ts'],
  },
});
