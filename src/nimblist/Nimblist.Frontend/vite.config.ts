import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import mkcert from 'vite-plugin-mkcert'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        react(),
        tailwindcss(),
        mkcert(),
        VitePWA({
            strategies: 'injectManifest',
            srcDir: 'src',
            filename: 'sw.ts',
            registerType: 'autoUpdate',
            includeAssets: ['favicon.ico', 'apple-touch-icon-180x180.png', 'pwa-icon.svg'],
            manifest: {
                name: 'Nimblist',
                short_name: 'Nimblist',
                description: 'Collaborative shopping lists for your household',
                theme_color: '#16a34a',
                background_color: '#ffffff',
                display: 'standalone',
                orientation: 'portrait',
                scope: '/',
                start_url: '/',
                icons: [
                    { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
                    { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
                    { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
                    { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
                ],
                share_target: {
                    action: '/share-target',
                    method: 'GET',
                    params: {
                        title: 'title',
                        text: 'text',
                        url: 'url',
                    },
                },
            },
        }),
    ],
        server: {
      // 3. Optional but recommended: Ensure Vite uses HTTPS
      https: true,
      // 4. Optional: Specify the port if needed (defaults might work)
      port: 5173,
      // 5. Optional: Prevent Vite trying other ports if 5173 is busy
      strictPort: true,
      // 6. Bind to all network interfaces so other machines can access
      host: '0.0.0.0',
    }
})
