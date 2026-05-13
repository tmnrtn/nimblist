/// <reference lib="webworker" />
import { precacheAndRoute } from 'workbox-precaching'

declare let self: ServiceWorkerGlobalScope

precacheAndRoute(self.__WB_MANIFEST)

self.addEventListener('push', (event) => {
    if (!event.data) return
    const data = event.data.json() as { title?: string; body?: string; url?: string }
    event.waitUntil(
        self.registration.showNotification(data.title ?? 'Nimblist', {
            body: data.body,
            icon: '/pwa-192x192.png',
            badge: '/pwa-64x64.png',
            data: { url: data.url ?? '/' },
        })
    )
})

self.addEventListener('notificationclick', (event) => {
    event.notification.close()
    const url = (event.notification.data as { url: string }).url
    event.waitUntil(
        self.clients
            .matchAll({ type: 'window', includeUncontrolled: true })
            .then((clientList) => {
                for (const client of clientList) {
                    if ('focus' in client) {
                        client.navigate(url)
                        return client.focus()
                    }
                }
                return self.clients.openWindow(url)
            })
    )
})
