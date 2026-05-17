import { Link } from 'react-router-dom';

export default function PrivacyPolicyPage() {
  return (
    <div className="max-w-2xl mx-auto py-8 px-4">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Privacy Policy</h1>
      <p className="text-sm text-gray-500 mb-8">Last updated: May 2026</p>

      <div className="prose prose-sm text-gray-700 space-y-6">
        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Who we are</h2>
          <p>
            Nimblist is a collaborative shopping list and recipe management application operated by Tom Norton
            (tmnrtn@gmail.com). References to "we", "us", or "our" in this policy refer to the operator.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">What data we collect</h2>
          <ul className="list-disc list-inside space-y-1">
            <li><strong>Account information:</strong> your email address and, if you registered with a password, a hashed version of it.</li>
            <li><strong>OAuth tokens:</strong> if you sign in with Google, Facebook, or Microsoft we receive your email address and profile name from that provider.</li>
            <li><strong>Content you create:</strong> shopping lists, items, recipes, meal plans, families, and tags.</li>
            <li><strong>Usage data:</strong> server logs (IP address, browser, request timestamps) for security and debugging purposes.</li>
            <li><strong>Payment data:</strong> if you subscribe, PayPal processes your payment. We store only your PayPal subscription ID and billing status — we never see your card details.</li>
            <li><strong>Push notifications:</strong> if you grant permission, we store your browser push subscription endpoint to deliver list-update notifications.</li>
          </ul>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">How we use your data</h2>
          <ul className="list-disc list-inside space-y-1">
            <li>To provide and operate the Nimblist service.</li>
            <li>To send transactional emails (account confirmation, subscription updates).</li>
            <li>To send push notifications about changes to shared lists (only with your explicit permission).</li>
            <li>To improve the classification and ingredient-parsing models (anonymised feedback only, if you submit corrections).</li>
          </ul>
          <p className="mt-2">We do not sell your data or use it for advertising.</p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Data storage and security</h2>
          <p>
            Data is stored in a PostgreSQL database hosted on a DigitalOcean Droplet in the EU region.
            Connections are encrypted in transit (TLS). Authentication cookies are set with the <code>Secure</code> and
            <code>SameSite=Strict</code> flags.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Third-party services</h2>
          <ul className="list-disc list-inside space-y-1">
            <li><strong>PayPal</strong> — payment processing. See <a href="https://www.paypal.com/uk/legalhub/privacy-full" className="text-indigo-600 hover:underline" target="_blank" rel="noreferrer">PayPal Privacy Policy</a>.</li>
            <li><strong>Resend</strong> — transactional email delivery. See <a href="https://resend.com/legal/privacy-policy" className="text-indigo-600 hover:underline" target="_blank" rel="noreferrer">Resend Privacy Policy</a>.</li>
            <li><strong>Google / Facebook / Microsoft</strong> — OAuth login providers (optional). Each provider's own privacy policy applies.</li>
          </ul>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Your rights (GDPR)</h2>
          <p>If you are based in the UK or EU, you have the right to:</p>
          <ul className="list-disc list-inside space-y-1 mt-2">
            <li><strong>Access</strong> — export all your data from the <Link to="/billing" className="text-indigo-600 hover:underline">Account &amp; Billing</Link> page.</li>
            <li><strong>Erasure</strong> — delete your account and all associated data from the same page.</li>
            <li><strong>Correction</strong> — update your email via the Identity pages.</li>
            <li><strong>Portability</strong> — your export is in standard JSON format.</li>
          </ul>
          <p className="mt-2">To exercise any other rights, email <a href="mailto:tmnrtn@gmail.com" className="text-indigo-600 hover:underline">tmnrtn@gmail.com</a>.</p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Cookies</h2>
          <p>
            We use a single first-party authentication cookie (<code>.AspNetCore.Identity.Application</code>) that expires after
            30 days of inactivity. No third-party tracking cookies are set.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Changes to this policy</h2>
          <p>
            We may update this policy from time to time. Continued use of Nimblist after a change constitutes
            acceptance of the updated policy.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">Contact</h2>
          <p>
            Questions? Email <a href="mailto:tmnrtn@gmail.com" className="text-indigo-600 hover:underline">tmnrtn@gmail.com</a>.
          </p>
        </section>
      </div>

      <div className="mt-8 text-center">
        <Link to="/" className="text-sm text-indigo-600 hover:underline">← Back to home</Link>
      </div>
    </div>
  );
}
