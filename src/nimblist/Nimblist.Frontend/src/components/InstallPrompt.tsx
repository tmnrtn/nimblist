import { useState, useEffect } from 'react'

interface BeforeInstallPromptEvent extends Event {
    prompt(): Promise<void>
    userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const DISMISSED_KEY = 'nimblist_install_dismissed'

export default function InstallPrompt() {
    const [iosPrompt, setIosPrompt] = useState(false)
    const [androidPrompt, setAndroidPrompt] = useState<BeforeInstallPromptEvent | null>(null)

    useEffect(() => {
        if (sessionStorage.getItem(DISMISSED_KEY)) return

        const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent)
        const isInStandaloneMode = window.matchMedia('(display-mode: standalone)').matches
            || ('standalone' in navigator && (navigator as Navigator & { standalone: boolean }).standalone)

        if (isIos && !isInStandaloneMode) {
            setIosPrompt(true)
            return
        }

        const handler = (e: Event) => {
            e.preventDefault()
            setAndroidPrompt(e as BeforeInstallPromptEvent)
        }
        window.addEventListener('beforeinstallprompt', handler)
        return () => window.removeEventListener('beforeinstallprompt', handler)
    }, [])

    const dismiss = () => {
        sessionStorage.setItem(DISMISSED_KEY, '1')
        setIosPrompt(false)
        setAndroidPrompt(null)
    }

    const installAndroid = async () => {
        if (!androidPrompt) return
        await androidPrompt.prompt()
        const { outcome } = await androidPrompt.userChoice
        if (outcome === 'accepted') setAndroidPrompt(null)
        else dismiss()
    }

    if (iosPrompt) {
        return (
            <div className="fixed bottom-0 left-0 right-0 z-50 m-4 rounded-2xl bg-white shadow-xl border border-gray-200 p-4">
                <div className="flex items-start gap-3">
                    <img src="/apple-touch-icon-180x180.png" alt="Nimblist" className="w-12 h-12 rounded-xl flex-shrink-0" />
                    <div className="flex-1 min-w-0">
                        <p className="font-semibold text-gray-900 text-sm">Install Nimblist</p>
                        <p className="text-gray-500 text-xs mt-1">
                            Tap the <strong>Share</strong> button{' '}
                            <span className="inline-block">
                                <svg className="inline w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                                    <path d="M12 2l-4 4h3v8h2V6h3l-4-4zm-7 14v4h14v-4h-2v2H7v-2H5z" />
                                </svg>
                            </span>{' '}
                            then <strong>"Add to Home Screen"</strong>
                        </p>
                    </div>
                    <button onClick={dismiss} className="text-gray-400 hover:text-gray-600 flex-shrink-0 p-1" aria-label="Dismiss">
                        <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>
        )
    }

    if (androidPrompt) {
        return (
            <div className="fixed bottom-0 left-0 right-0 z-50 m-4 rounded-2xl bg-white shadow-xl border border-gray-200 p-4">
                <div className="flex items-center gap-3">
                    <img src="/pwa-192x192.png" alt="Nimblist" className="w-12 h-12 rounded-xl flex-shrink-0" />
                    <div className="flex-1 min-w-0">
                        <p className="font-semibold text-gray-900 text-sm">Install Nimblist</p>
                        <p className="text-gray-500 text-xs mt-0.5">Add to your home screen for quick access</p>
                    </div>
                    <div className="flex gap-2 flex-shrink-0">
                        <button onClick={dismiss} className="text-sm text-gray-500 hover:text-gray-700 px-2 py-1">
                            Not now
                        </button>
                        <button onClick={installAndroid} className="text-sm bg-green-600 hover:bg-green-700 text-white px-3 py-1 rounded-lg font-medium">
                            Install
                        </button>
                    </div>
                </div>
            </div>
        )
    }

    return null
}
