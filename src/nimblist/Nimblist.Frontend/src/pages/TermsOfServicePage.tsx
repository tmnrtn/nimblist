import { Link } from 'react-router-dom';

export default function TermsOfServicePage() {
  return (
    <div className="max-w-2xl mx-auto py-8 px-4">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Terms of Service</h1>
      <p className="text-sm text-gray-500 mb-8">Last updated: May 2026</p>

      <div className="prose prose-sm text-gray-700 space-y-6">
        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">1. Acceptance</h2>
          <p>
            By creating an account or using Nimblist you agree to these Terms of Service. If you do not agree,
            do not use the service.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">2. The service</h2>
          <p>
            Nimblist provides a collaborative shopping list, recipe management, and meal planning tool. The
            service is provided on an "as is" and "as available" basis. We make no guarantees of uptime or
            data durability beyond reasonable commercial efforts.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">3. Free and paid tiers</h2>
          <p>
            Nimblist offers a free tier with a limited number of saved recipes and a paid tier (Premium) that
            unlocks additional features. The paid tier is billed monthly at £1.99 via PayPal and includes a
            7-day free trial. You may cancel at any time; access continues until the end of the current
            billing period.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">4. Your content</h2>
          <p>
            You retain ownership of all content you create (lists, recipes, etc.). You grant us a limited
            licence to store and process it solely to operate the service. We will not share your content with
            third parties except as required to deliver the service (e.g., email delivery via Resend).
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">5. Acceptable use</h2>
          <p>You agree not to:</p>
          <ul className="list-disc list-inside space-y-1 mt-2">
            <li>Use Nimblist for any unlawful purpose.</li>
            <li>Attempt to circumvent security controls or rate limits.</li>
            <li>Share your account credentials.</li>
            <li>Scrape or automate access to the service beyond normal use.</li>
          </ul>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">6. Account termination</h2>
          <p>
            You may delete your account at any time from the <Link to="/billing" className="text-indigo-600 hover:underline">Account &amp; Billing</Link> page.
            We reserve the right to suspend or terminate accounts that violate these terms.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">7. Limitation of liability</h2>
          <p>
            To the fullest extent permitted by law, Nimblist and its operator are not liable for any indirect,
            incidental, or consequential damages arising from your use of the service, including data loss.
            We strongly recommend you use the data export feature to keep your own copies of important data.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">8. Governing law</h2>
          <p>These terms are governed by the laws of England and Wales.</p>
        </section>

        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-2">9. Changes</h2>
          <p>
            We may revise these terms at any time. Continued use of the service after a change constitutes
            acceptance of the revised terms.
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
