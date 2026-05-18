import { useState, useCallback } from 'react';

type ConsentValue = 'accepted' | 'declined' | null;

const STORAGE_KEY = 'nimblist_analytics_consent';

function readStored(): ConsentValue {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    if (v === 'accepted' || v === 'declined') return v;
  } catch { /* ignore */ }
  return null;
}

export function useCookieConsent() {
  const [consent, setConsent] = useState<ConsentValue>(readStored);

  const accept = useCallback(() => {
    try { localStorage.setItem(STORAGE_KEY, 'accepted'); } catch { /* ignore */ }
    setConsent('accepted');
  }, []);

  const decline = useCallback(() => {
    try { localStorage.setItem(STORAGE_KEY, 'declined'); } catch { /* ignore */ }
    setConsent('declined');
  }, []);

  return { consent, accept, decline };
}
