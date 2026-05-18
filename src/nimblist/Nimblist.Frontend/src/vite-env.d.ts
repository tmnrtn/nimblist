/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/client" />

interface Window {
  gtag?: (...args: unknown[]) => void;
  dataLayer?: unknown[];
}
