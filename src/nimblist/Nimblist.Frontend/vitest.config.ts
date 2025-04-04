import { defineConfig } from 'vitest/config';

export default defineConfig({
  // ... other config
  test: {
    globals: true, // <--- MAKE SURE THIS IS TRUE
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts', // Your setup file path
    // ... other test options
  },
});
