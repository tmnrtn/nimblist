import { defineConfig } from 'vitest/config';

export default defineConfig({
  // ... other config
  test: {
    globals: true, // <--- MAKE SURE THIS IS TRUE
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts', // Your setup file path
    // ... other test options
    coverage: {
      provider: 'v8', // or 'istanbul' if you installed that
      reporter: ['text', 'json', 'html', 'lcov'], // *** Crucial: Ensure 'lcov' is included ***
      reportsDirectory: './coverage', // Default output directory
      // Optional: Specify files to include/exclude from coverage
      include: ['src/**/*.{js,jsx,ts,tsx}'],
      exclude: [
        'src/main.tsx', // Often exclude the main entry point
        'src/**/*.d.ts', // TypeScript definition files
        'src/**/index.{js,ts}', // Barrel files if you have them
        'src/setupTests.js', // Test setup file
        // Add any other files/patterns to exclude (e.g., constants, types)
      ],
      // Optional: Set coverage thresholds (Vitest will fail if not met)
      // thresholds: {
      //   lines: 80,
      //   functions: 80,
      //   branches: 80,
      //   statements: 80,
      // },
    },
  },
});
