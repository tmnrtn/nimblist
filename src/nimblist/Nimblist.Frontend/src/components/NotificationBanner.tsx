import { useState } from 'react'
import { usePushNotifications } from '../hooks/usePushNotifications'

const DISMISSED_KEY = 'nimblist_notif_dismissed'

export default function NotificationBanner() {
    const { permission, subscribe } = usePushNotifications()
    const [dismissed, setDismissed] = useState(() => !!localStorage.getItem(DISMISSED_KEY))
    const [busy, setBusy] = useState(false)

    if (dismissed || permission === 'unsupported' || permission === 'granted' || permission === 'denied') {
        return null
    }

    const dismiss = () => {
        localStorage.setItem(DISMISSED_KEY, '1')
        setDismissed(true)
    }

    const handleEnable = async () => {
        setBusy(true)
        await subscribe()
        setBusy(false)
        dismiss()
    }

    return (
        <div className="fixed bottom-0 left-0 right-0 z-40 m-4 rounded-2xl bg-white shadow-xl border border-gray-200 p-4">
            <div className="flex items-center gap-3">
                <div className="text-2xl flex-shrink-0">🔔</div>
                <div className="flex-1 min-w-0">
                    <p className="font-semibold text-gray-900 text-sm">Get notified when items are added</p>
                    <p className="text-gray-500 text-xs mt-0.5">We'll notify you when someone adds to a shared list</p>
                </div>
                <div className="flex gap-2 flex-shrink-0">
                    <button onClick={dismiss} className="text-sm text-gray-500 hover:text-gray-700 px-2 py-1">
                        Not now
                    </button>
                    <button
                        onClick={handleEnable}
                        disabled={busy}
                        className="text-sm bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white px-3 py-1 rounded-lg font-medium"
                    >
                        Enable
                    </button>
                </div>
            </div>
        </div>
    )
}
