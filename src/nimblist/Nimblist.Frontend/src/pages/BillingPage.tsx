import { useEffect, useRef, useState } from 'react';
import { usePageTitle } from '../hooks/usePageTitle';
import { Link } from 'react-router-dom';
import { PayPalScriptProvider, PayPalButtons } from '@paypal/react-paypal-js';
import useAuthStore from '../store/authStore';
import { authenticatedFetch } from '../components/HttpHelper';

interface SubscriptionStatus {
  tier: string;
  status: string | null;
  isInTrial: boolean;
  trialEndDate: string | null;
  nextBillingDate: string | null;
  payPalSubscriptionId: string | null;
}

interface PayPalConfig {
  clientId: string;
  planId: string;
}

function daysUntil(dateStr: string): number {
  const diff = new Date(dateStr).getTime() - Date.now();
  return Math.max(0, Math.ceil(diff / (1000 * 60 * 60 * 24)));
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' });
}

export default function BillingPage() {
  usePageTitle('Account & Billing');
  const { checkAuthStatus } = useAuthStore();
  const [sub, setSub] = useState<SubscriptionStatus | null>(null);
  const [config, setConfig] = useState<PayPalConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [cancelling, setCancelling] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [subRes, cfgRes] = await Promise.all([
          authenticatedFetch('/api/subscription'),
          authenticatedFetch('/api/subscription/config'),
        ]);
        if (subRes.ok) setSub(await subRes.json());
        if (cfgRes.ok) setConfig(await cfgRes.json());
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  async function handleActivate(subscriptionId: string) {
    const res = await authenticatedFetch('/api/subscription/activate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ subscriptionId }),
    });

    if (res.ok) {
      const updated: SubscriptionStatus = await res.json();
      setSub(updated);
      setSuccess('Subscription activated! Enjoy Nimblist Premium.');
      await checkAuthStatus();
    } else {
      const body = await res.json().catch(() => ({}));
      setError(body.error ?? 'Failed to activate subscription. Please contact support.');
    }
  }

  async function handleCancel() {
    if (!confirm('Are you sure you want to cancel your subscription? You will lose access to premium features at the end of your current billing period.')) return;
    setCancelling(true);
    setError(null);

    const res = await authenticatedFetch('/api/subscription/cancel', { method: 'POST' });
    if (res.ok) {
      setSuccess('Your subscription has been cancelled.');
      setSub(prev => prev ? { ...prev, tier: 'free', status: 'CANCELLED' } : prev);
      await checkAuthStatus();
    } else {
      setError('Failed to cancel subscription. Please try again or contact support.');
    }
    setCancelling(false);
  }

  if (loading) {
    return <div className="flex justify-center py-12 text-gray-500">Loading billing information...</div>;
  }

  const isPaid = sub?.tier === 'paid';
  const isActive = sub?.status === 'ACTIVE' || sub?.status === 'APPROVED';

  return (
    <div className="max-w-2xl mx-auto py-8 px-4">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Account &amp; Billing</h1>

      {error && (
        <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded text-red-700 text-sm">{error}</div>
      )}
      {success && (
        <div className="mb-4 p-4 bg-green-50 border border-green-200 rounded text-green-700 text-sm">{success}</div>
      )}

      {/* Current plan */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-800">Current Plan</h2>
          <span className={`px-3 py-1 rounded-full text-sm font-medium ${isPaid && isActive ? 'bg-indigo-100 text-indigo-700' : 'bg-gray-100 text-gray-600'}`}>
            {isPaid && isActive ? 'Premium' : 'Free'}
          </span>
        </div>

        {isPaid && isActive ? (
          <div className="space-y-2 text-sm text-gray-600">
            {sub?.isInTrial && sub.trialEndDate && (
              <p className="text-indigo-600 font-medium">
                Free trial — {daysUntil(sub.trialEndDate)} day{daysUntil(sub.trialEndDate) !== 1 ? 's' : ''} remaining (ends {formatDate(sub.trialEndDate)})
              </p>
            )}
            {sub?.nextBillingDate && (
              <p>Next billing date: <span className="font-medium text-gray-800">{formatDate(sub.nextBillingDate)}</span> — £1.99</p>
            )}
            <ul className="mt-3 space-y-1 text-gray-700">
              <li className="flex items-center gap-2"><span className="text-green-500">✓</span> Unlimited recipes</li>
              <li className="flex items-center gap-2"><span className="text-green-500">✓</span> Recipe import from URL &amp; image</li>
              <li className="flex items-center gap-2"><span className="text-green-500">✓</span> Meal Planner</li>
            </ul>
          </div>
        ) : (
          <div className="space-y-2 text-sm text-gray-600">
            {sub?.status === 'CANCELLED' && (
              <p className="text-orange-600 font-medium">Your subscription has been cancelled.</p>
            )}
            <ul className="mt-2 space-y-1">
              <li className="flex items-center gap-2"><span className="text-green-500">✓</span> Up to 25 saved recipes</li>
              <li className="flex items-center gap-2"><span className="text-green-500">✓</span> Shopping lists &amp; families</li>
              <li className="flex items-center gap-2"><span className="text-gray-400">✗</span> Recipe import from URL &amp; image</li>
              <li className="flex items-center gap-2"><span className="text-gray-400">✗</span> Meal Planner</li>
            </ul>
          </div>
        )}
      </div>

      {/* Invite friends */}
      <InvitePanel />

      {/* Upgrade CTA */}
      {(!isPaid || !isActive) && config?.clientId && config?.planId && (
        <div className="bg-indigo-50 border border-indigo-200 rounded-lg p-6">
          <h2 className="text-lg font-semibold text-indigo-900 mb-1">Upgrade to Premium</h2>
          <p className="text-sm text-indigo-700 mb-1">£1.99 / month — includes a 7-day free trial. Cancel anytime.</p>
          <p className="text-xs text-indigo-500 mb-4">Your card will not be charged until the trial ends.</p>
          <PayPalScriptProvider options={{ clientId: config.clientId, vault: true, intent: 'subscription' }}>
            <PayPalButtons
              style={{ layout: 'vertical', color: 'blue', shape: 'rect', label: 'subscribe' }}
              createSubscription={(_data, actions) =>
                actions.subscription.create({ plan_id: config.planId })
              }
              onApprove={async (data) => {
                if (data.subscriptionID) await handleActivate(data.subscriptionID);
              }}
              onError={(err) => {
                console.error('PayPal error', err);
                setError('Something went wrong with PayPal. Please try again.');
              }}
            />
          </PayPalScriptProvider>
        </div>
      )}

      {/* Cancel */}
      {isPaid && isActive && (
        <div className="mt-6 pt-6 border-t border-gray-200">
          <h2 className="text-sm font-semibold text-gray-700 mb-2">Cancel Subscription</h2>
          <p className="text-sm text-gray-500 mb-3">
            Cancelling will stop future payments. You'll keep premium access until the end of your current billing period.
          </p>
          <button
            onClick={handleCancel}
            disabled={cancelling}
            className="px-4 py-2 text-sm font-medium text-red-600 border border-red-300 rounded hover:bg-red-50 disabled:opacity-50 transition-colors"
          >
            {cancelling ? 'Cancelling...' : 'Cancel Subscription'}
          </button>
        </div>
      )}

      {/* Data & Privacy */}
      <div className="mt-8 pt-6 border-t border-gray-200 space-y-6">
        <h2 className="text-sm font-semibold text-gray-700">Your Data</h2>

        <div>
          <p className="text-sm text-gray-500 mb-2">
            Download a copy of all your data — lists, recipes, and meal plans.
          </p>
          <a
            href="/api/auth/export"
            className="inline-block px-4 py-2 text-sm font-medium text-indigo-600 border border-indigo-300 rounded hover:bg-indigo-50 transition-colors"
          >
            Export my data
          </a>
        </div>

        <div>
          <p className="text-sm text-gray-500 mb-2">
            Permanently delete your account and all associated data. This cannot be undone.
          </p>
          <DeleteAccountButton />
        </div>

        <div className="text-xs text-gray-400 space-x-3">
          <Link to="/privacy" className="hover:underline">Privacy Policy</Link>
          <Link to="/terms" className="hover:underline">Terms of Service</Link>
        </div>
      </div>

      <div className="mt-6 text-center">
        <Link to="/" className="text-sm text-indigo-600 hover:underline">← Back to home</Link>
      </div>
    </div>
  );
}

function InvitePanel() {
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [emailInput, setEmailInput] = useState('');
  const [sending, setSending] = useState(false);
  const [sendResult, setSendResult] = useState<{ ok: boolean; message: string } | null>(null);
  const copyTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const resultTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    authenticatedFetch('/api/auth/invite')
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data?.inviteUrl) setInviteUrl(data.inviteUrl); })
      .catch(() => {});
    return () => {
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
      if (resultTimeoutRef.current) clearTimeout(resultTimeoutRef.current);
    };
  }, []);

  function handleCopy() {
    if (!inviteUrl) return;
    navigator.clipboard.writeText(inviteUrl).then(() => {
      setCopied(true);
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
      copyTimeoutRef.current = setTimeout(() => setCopied(false), 2000);
    });
  }

  async function handleSendEmail(e: React.FormEvent) {
    e.preventDefault();
    if (!emailInput.trim()) return;
    setSending(true);
    setSendResult(null);
    try {
      const res = await authenticatedFetch('/api/auth/invite/send', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: emailInput.trim() }),
      });
      if (res.ok) {
        setSendResult({ ok: true, message: `Invite sent to ${emailInput.trim()}` });
        setEmailInput('');
      } else {
        const body = await res.json().catch(() => ({}));
        setSendResult({ ok: false, message: body.error ?? 'Failed to send invite.' });
      }
    } catch {
      setSendResult({ ok: false, message: 'Failed to send invite.' });
    } finally {
      setSending(false);
      if (resultTimeoutRef.current) clearTimeout(resultTimeoutRef.current);
      resultTimeoutRef.current = setTimeout(() => setSendResult(null), 4000);
    }
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
      <h2 className="text-lg font-semibold text-gray-800 mb-1">Invite friends &amp; family</h2>
      <p className="text-sm text-gray-500 mb-4">
        Share your invite link or send an invitation by email.
      </p>
      {inviteUrl ? (
        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <input
              readOnly
              value={inviteUrl}
              className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded bg-gray-50 text-gray-700 truncate"
            />
            <button
              onClick={handleCopy}
              className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded hover:bg-indigo-700 transition-colors whitespace-nowrap"
            >
              {copied ? 'Copied!' : 'Copy link'}
            </button>
          </div>
          <form onSubmit={handleSendEmail} className="flex items-center gap-2">
            <input
              type="email"
              value={emailInput}
              onChange={e => setEmailInput(e.target.value)}
              placeholder="friend@example.com"
              required
              className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-indigo-300"
            />
            <button
              type="submit"
              disabled={sending || !emailInput.trim()}
              className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded hover:bg-indigo-700 disabled:opacity-50 transition-colors whitespace-nowrap"
            >
              {sending ? 'Sending…' : 'Send invite'}
            </button>
          </form>
          {sendResult && (
            <p className={`text-sm ${sendResult.ok ? 'text-green-600' : 'text-red-600'}`}>
              {sendResult.message}
            </p>
          )}
        </div>
      ) : (
        <p className="text-sm text-gray-400">Loading invite link...</p>
      )}
    </div>
  );
}

function DeleteAccountButton() {
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleDelete() {
    setDeleting(true);
    setError(null);
    const res = await authenticatedFetch('/api/auth/account', { method: 'DELETE' });
    if (res.ok) {
      window.location.href = '/';
    } else {
      setError('Failed to delete account. Please contact support.');
      setDeleting(false);
      setConfirming(false);
    }
  }

  if (!confirming) {
    return (
      <button
        onClick={() => setConfirming(true)}
        className="px-4 py-2 text-sm font-medium text-red-600 border border-red-300 rounded hover:bg-red-50 transition-colors"
      >
        Delete my account
      </button>
    );
  }

  return (
    <div className="p-4 bg-red-50 border border-red-200 rounded space-y-3">
      <p className="text-sm font-medium text-red-800">Are you sure? This will permanently delete your account and all data.</p>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex gap-3">
        <button
          onClick={handleDelete}
          disabled={deleting}
          className="px-4 py-2 text-sm font-medium bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50 transition-colors"
        >
          {deleting ? 'Deleting...' : 'Yes, delete my account'}
        </button>
        <button
          onClick={() => setConfirming(false)}
          className="px-4 py-2 text-sm font-medium text-gray-600 border border-gray-300 rounded hover:bg-gray-50 transition-colors"
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
