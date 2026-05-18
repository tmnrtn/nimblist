import { lazy, Suspense, useEffect } from 'react';
import { Routes, Route, useLocation } from 'react-router-dom';
import './App.css'

import useAuthStore from './store/authStore';
import { authenticatedFetch } from './components/HttpHelper';
import { useCookieConsent } from './hooks/useCookieConsent';

import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import AdminRoute from './components/AdminRoute';
import InstallPrompt from './components/InstallPrompt';
import NotificationBanner from './components/NotificationBanner';
import CookieBanner from './components/CookieBanner';

// Hot path — stay in main bundle
import HomePage from './pages/HomePage';
import ShoppingListsPage from './pages/ShoppingListsPage';
import ListPageDetail from './pages/ListPageDetail';
import NotFoundPage from './pages/NotFoundPage';

// Lazy-loaded routes — split into separate chunks
const RecipesPage        = lazy(() => import('./pages/RecipesPage'));
const RecipeDetailPage   = lazy(() => import('./pages/RecipeDetailPage'));
const FamiliesPage       = lazy(() => import('./pages/FamiliesPage'));
const MealPlannerPage    = lazy(() => import('./pages/MealPlannerPage'));
const BillingPage        = lazy(() => import('./pages/BillingPage'));
const AdminPage          = lazy(() => import('./pages/AdminPage'));
const PreviousItemNamesPage = lazy(() => import('./pages/PreviousItemNamesPage'));
const PrivacyPolicyPage  = lazy(() => import('./pages/PrivacyPolicyPage'));
const TermsOfServicePage = lazy(() => import('./pages/TermsOfServicePage'));
const ShareTargetPage    = lazy(() => import('./pages/ShareTargetPage'));

function PageSpinner() {
  return (
    <div className="flex justify-center items-center py-24">
      <svg className="animate-spin h-6 w-6 text-indigo-600" viewBox="0 0 24 24" fill="none">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
      </svg>
    </div>
  );
}

function App() {
    const { checkAuthStatus, isLoading, isAuthenticated } = useAuthStore();
    const location = useLocation();
    const { consent, accept, decline } = useCookieConsent();

    // Load GA only after explicit consent
    useEffect(() => {
      if (consent !== 'accepted') return;
      const id = import.meta.env.VITE_GA_MEASUREMENT_ID;
      if (!id || typeof window.gtag === 'function') return;
      const script = document.createElement('script');
      script.async = true;
      script.src = `https://www.googletagmanager.com/gtag/js?id=${id}`;
      document.head.appendChild(script);
      window.dataLayer = window.dataLayer || [];
      const gtag = (...args: unknown[]) => { window.dataLayer!.push(args); };
      window.gtag = gtag;
      gtag('js', new Date());
      gtag('config', id, { send_page_view: false });
    }, [consent]);

    // Track page views (only fires when gtag is initialised, i.e. consent given)
    useEffect(() => {
      if (typeof window.gtag !== 'function') return;
      window.gtag('event', 'page_view', {
        page_path: location.pathname + location.search,
        page_title: document.title,
      });
    }, [location.pathname, location.search]);

    useEffect(() => {
      const params = new URLSearchParams(window.location.search);
      const inviteCode = params.get('invite');
      if (inviteCode) localStorage.setItem('nimblist_invite_code', inviteCode);
      checkAuthStatus();
    }, [checkAuthStatus]);

    // Claim any stored invite code once the user is authenticated
    useEffect(() => {
      if (!isAuthenticated || isLoading) return;
      const code = localStorage.getItem('nimblist_invite_code');
      if (!code) return;
      localStorage.removeItem('nimblist_invite_code');
      authenticatedFetch('/api/auth/claim-invite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code }),
      }).catch(() => {});
    }, [isAuthenticated, isLoading]);
  
    // Show loading indicator until the initial check is complete
    if (isLoading) {
      return (
        <div className="flex justify-center items-center min-h-screen">
           {/* You can replace this with a proper spinner component */}
           Loading Application...
        </div>
        );
    }

  return (
    <>
    <InstallPrompt />
    <NotificationBanner />
    {consent === null && <CookieBanner onAccept={accept} onDecline={decline} />}
    <Suspense fallback={<PageSpinner />}>
    <Routes> {/* Container for all routes */}
      {/* Define the parent route that uses the Layout */}
      <Route path="/" element={<Layout />}>
        {/* Define child routes that render inside the Layout's <Outlet /> */}

        {/* 'index' specifies the default component for the parent's path ("/") */}
        <Route index element={<HomePage />} />

        {/* Option 2: Using a dedicated ProtectedRoute component is cleaner later */}
        <Route element={<ProtectedRoute />}>
            <Route path="lists" element={<ShoppingListsPage />} />
            <Route path="lists/:listId" element={<ListPageDetail />} />
            <Route path="previous-item-names" element={<PreviousItemNamesPage />} />
            <Route path="recipes" element={<RecipesPage />} />
            <Route path="recipes/:recipeId" element={<RecipeDetailPage />} />
            <Route path="families" element={<FamiliesPage />} />
            <Route path="meal-planner" element={<MealPlannerPage />} />
            <Route path="billing" element={<BillingPage />} />
            <Route path="share-target" element={<ShareTargetPage />} />
        </Route>

        <Route element={<AdminRoute />}>
            <Route path="admin" element={<AdminPage />} />
        </Route>

        {/* Public routes */}
        <Route path="privacy" element={<PrivacyPolicyPage />} />
        <Route path="terms" element={<TermsOfServicePage />} />

        {/* Catch-all route for any paths not matched above */}
        <Route path="*" element={<NotFoundPage />} />

      </Route> {/* End of routes using the Layout */}

    </Routes>
    </Suspense>
    </>
  )
}

export default App
