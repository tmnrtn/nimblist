import { useState, useEffect } from 'react'
import { authenticatedFetch } from '../components/HttpHelper'

const VAPID_PUBLIC_KEY = import.meta.env.VITE_VAPID_PUBLIC_KEY as string

function urlBase64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
    const rawData = atob(base64)
    return Uint8Array.from([...rawData].map((c) => c.charCodeAt(0)))
}

export type NotificationPermission = 'default' | 'granted' | 'denied' | 'unsupported'

export function usePushNotifications() {
    const [permission, setPermission] = useState<NotificationPermission>('unsupported')
    const [subscribed, setSubscribed] = useState(false)

    useEffect(() => {
        if (!('Notification' in window) || !('serviceWorker' in navigator) || !('PushManager' in window)) return
        setPermission(Notification.permission)
    }, [])

    const subscribe = async (): Promise<boolean> => {
        if (!('serviceWorker' in navigator) || !VAPID_PUBLIC_KEY) return false

        const result = await Notification.requestPermission()
        setPermission(result)
        if (result !== 'granted') return false

        try {
            const registration = await navigator.serviceWorker.ready
            const sub = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(VAPID_PUBLIC_KEY),
            })
            const json = sub.toJSON()
            await authenticatedFetch('/api/pushsubscriptions', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ endpoint: json.endpoint, keys: json.keys }),
            })
            setSubscribed(true)
            return true
        } catch {
            return false
        }
    }

    const unsubscribe = async () => {
        if (!('serviceWorker' in navigator)) return
        const registration = await navigator.serviceWorker.ready
        const sub = await registration.pushManager.getSubscription()
        if (!sub) return
        const json = sub.toJSON()
        await authenticatedFetch('/api/pushsubscriptions', {
            method: 'DELETE',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ endpoint: json.endpoint, keys: json.keys }),
        })
        await sub.unsubscribe()
        setSubscribed(false)
    }

    return { permission, subscribed, subscribe, unsubscribe }
}
