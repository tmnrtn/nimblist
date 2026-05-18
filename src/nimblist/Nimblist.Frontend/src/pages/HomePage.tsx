import { useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import useAuthStore from '../store/authStore';
import { usePageTitle } from '../hooks/usePageTitle';

export default function HomePage() {
  const { isAuthenticated } = useAuthStore();
  const navigate = useNavigate();
  const backendUrl = import.meta.env.VITE_API_BASE_URL ?? '';
  usePageTitle();

  useEffect(() => {
    if (!isAuthenticated) return;
    const lastListId = localStorage.getItem('nimblist_last_list');
    navigate(lastListId ? `/lists/${lastListId}` : '/lists', { replace: true });
  }, [isAuthenticated, navigate]);

  if (isAuthenticated) return null;

  const registerUrl = `${backendUrl}/Identity/Account/Register`;
  const loginUrl = `${backendUrl}/Identity/Account/Login`;

  return (
    <div>
      {/* Hero */}
      <section className="-mx-4 -mt-6 px-6 pt-20 pb-24 bg-gradient-to-br from-blue-600 via-indigo-600 to-indigo-800 text-white text-center">
        <div className="max-w-2xl mx-auto">
          <h2 className="text-4xl sm:text-5xl font-bold leading-tight mb-4">
            Shopping lists and recipes,<br className="hidden sm:block" /> perfectly in sync
          </h2>
          <p className="text-lg text-blue-100 mb-8 max-w-xl mx-auto">
            Share lists with your household, save recipes from anywhere, and plan your week — all in one place.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <a
              href={registerUrl}
              className="px-7 py-3 bg-green-500 hover:bg-green-400 text-white font-semibold rounded-lg shadow-md transition-colors text-base"
            >
              Start for free →
            </a>
            <a
              href={loginUrl}
              className="px-7 py-3 bg-white/10 hover:bg-white/20 text-white font-medium rounded-lg border border-white/30 transition-colors text-base"
            >
              Sign in
            </a>
          </div>
          <p className="mt-5 text-sm text-blue-200">No credit card required · 7-day free trial on Premium</p>
        </div>
      </section>

      {/* Features */}
      <section className="py-16">
        <h3 className="text-2xl font-bold text-center text-gray-800 mb-12">Everything your household needs</h3>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-10">
          <div className="text-center space-y-3">
            <div className="text-5xl mb-2">🛒</div>
            <h4 className="font-semibold text-gray-800 text-lg">Shared shopping lists</h4>
            <p className="text-sm text-gray-500 leading-relaxed">
              Real-time updates across your whole household. Check off items together and see changes the moment they happen, no refresh needed.
            </p>
          </div>
          <div className="text-center space-y-3">
            <div className="text-5xl mb-2">📖</div>
            <h4 className="font-semibold text-gray-800 text-lg">Recipe library</h4>
            <p className="text-sm text-gray-500 leading-relaxed">
              Import recipes from any website, photograph a recipe card, or create your own. Add all the ingredients to your shopping list in one tap.
            </p>
          </div>
          <div className="text-center space-y-3">
            <div className="text-5xl mb-2">📅</div>
            <h4 className="font-semibold text-gray-800 text-lg">Meal planner</h4>
            <p className="text-sm text-gray-500 leading-relaxed">
              Plan your meals day by day. Add a whole week's ingredients to your shopping list in a single click and never wonder what's for dinner.
            </p>
          </div>
        </div>
      </section>

      {/* How it works — simple 3-step */}
      <section className="-mx-4 px-4 py-16 bg-indigo-50">
        <h3 className="text-2xl font-bold text-center text-gray-800 mb-12">Get started in minutes</h3>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-8 max-w-3xl mx-auto text-center">
          <div className="space-y-2">
            <div className="w-10 h-10 rounded-full bg-indigo-600 text-white font-bold text-lg flex items-center justify-center mx-auto">1</div>
            <h4 className="font-semibold text-gray-800">Create an account</h4>
            <p className="text-sm text-gray-500">Sign up free with email, Google, or Microsoft in under a minute.</p>
          </div>
          <div className="space-y-2">
            <div className="w-10 h-10 rounded-full bg-indigo-600 text-white font-bold text-lg flex items-center justify-center mx-auto">2</div>
            <h4 className="font-semibold text-gray-800">Add your household</h4>
            <p className="text-sm text-gray-500">Invite family members to a Family — your lists and recipes update for everyone in real time.</p>
          </div>
          <div className="space-y-2">
            <div className="w-10 h-10 rounded-full bg-indigo-600 text-white font-bold text-lg flex items-center justify-center mx-auto">3</div>
            <h4 className="font-semibold text-gray-800">Start shopping smarter</h4>
            <p className="text-sm text-gray-500">Build lists, save recipes, and plan your week — all from your phone or browser.</p>
          </div>
        </div>
      </section>

      {/* Pricing */}
      <section className="py-16">
        <h3 className="text-2xl font-bold text-center text-gray-800 mb-2">Simple, honest pricing</h3>
        <p className="text-center text-sm text-gray-500 mb-10">Cancel anytime. No hidden fees.</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 max-w-2xl mx-auto">

          {/* Free tier */}
          <div className="bg-white rounded-2xl border border-gray-200 p-7 flex flex-col gap-5 shadow-sm">
            <div>
              <h4 className="text-lg font-bold text-gray-800">Free</h4>
              <p className="text-4xl font-bold text-gray-900 mt-2">£0</p>
            </div>
            <ul className="space-y-2.5 text-sm text-gray-600 flex-1">
              <li className="flex items-center gap-2"><span className="text-green-500 font-bold">✓</span> Unlimited shopping lists</li>
              <li className="flex items-center gap-2"><span className="text-green-500 font-bold">✓</span> Real-time family sharing</li>
              <li className="flex items-center gap-2"><span className="text-green-500 font-bold">✓</span> Up to 25 saved recipes</li>
              <li className="flex items-center gap-2"><span className="text-green-500 font-bold">✓</span> Push notifications</li>
              <li className="flex items-center gap-2 text-gray-400"><span className="font-bold">✗</span> Recipe import from URL or image</li>
              <li className="flex items-center gap-2 text-gray-400"><span className="font-bold">✗</span> Meal planner</li>
            </ul>
            <a
              href={registerUrl}
              className="block text-center px-4 py-2.5 border border-indigo-600 text-indigo-600 rounded-lg hover:bg-indigo-50 transition-colors font-semibold text-sm"
            >
              Get started free
            </a>
          </div>

          {/* Premium tier */}
          <div className="bg-indigo-600 rounded-2xl p-7 flex flex-col gap-5 text-white shadow-lg relative overflow-hidden">
            <div className="absolute top-4 right-4">
              <span className="bg-green-400 text-green-900 text-xs font-bold px-2.5 py-0.5 rounded-full">7-day free trial</span>
            </div>
            <div>
              <h4 className="text-lg font-bold">Premium</h4>
              <div className="mt-2">
                <span className="text-4xl font-bold">£1.99</span>
                <span className="text-indigo-100 text-base"> / month</span>
              </div>
            </div>
            <ul className="space-y-2.5 text-sm text-indigo-100 flex-1">
              <li className="flex items-center gap-2"><span className="text-green-400 font-bold">✓</span> Everything in Free</li>
              <li className="flex items-center gap-2"><span className="text-green-400 font-bold">✓</span> Unlimited recipes</li>
              <li className="flex items-center gap-2"><span className="text-green-400 font-bold">✓</span> Import from any recipe website</li>
              <li className="flex items-center gap-2"><span className="text-green-400 font-bold">✓</span> Import from a photo or image</li>
              <li className="flex items-center gap-2"><span className="text-green-400 font-bold">✓</span> Meal planner</li>
            </ul>
            <a
              href={registerUrl}
              className="block text-center px-4 py-2.5 bg-white text-indigo-600 rounded-lg hover:bg-indigo-50 transition-colors font-bold text-sm"
            >
              Start free trial
            </a>
          </div>

        </div>
      </section>

      {/* Bottom CTA */}
      <section className="-mx-4 px-4 py-16 bg-gradient-to-r from-blue-600 to-indigo-700 text-white text-center">
        <h3 className="text-2xl font-bold mb-3">Ready to get organised?</h3>
        <p className="text-blue-100 mb-7 text-sm max-w-sm mx-auto">
          Create your free account and start building smarter shopping lists today.
        </p>
        <a
          href={registerUrl}
          className="inline-block px-8 py-3 bg-green-500 hover:bg-green-400 text-white font-bold rounded-lg shadow transition-colors"
        >
          Create your free account
        </a>
        <p className="mt-4 text-xs text-blue-200">
          Already have an account?{' '}
          <a href={loginUrl} className="underline hover:text-white">Sign in</a>
        </p>
      </section>

      {/* Footer links */}
      <div className="py-6 text-center text-xs text-gray-400 space-x-4">
        <Link to="/privacy" className="hover:underline">Privacy Policy</Link>
        <Link to="/terms" className="hover:underline">Terms of Service</Link>
        <a href="mailto:support@nimblist.co.uk" className="hover:underline">support@nimblist.co.uk</a>
      </div>
    </div>
  );
}
