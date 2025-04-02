import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import mkcert from 'vite-plugin-mkcert' // 1. Import the plugin

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        react(),
        tailwindcss(),
        mkcert()
    ],
    server: {
      // 3. Optional but recommended: Ensure Vite uses HTTPS
      https: true,
      // 4. Optional: Specify the port if needed (defaults might work)
      port: 5173,
      // 5. Optional: Prevent Vite trying other ports if 5173 is busy
      strictPort: true,
    }
})
