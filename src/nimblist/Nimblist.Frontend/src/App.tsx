import { useEffect } from 'react'; // Add useEffect
import { Routes, Route, useLocation } from 'react-router-dom';
import './App.css'

import useAuthStore from './store/authStore'; // Import the auth store
import { authenticatedFetch } from './components/HttpHelper';

import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import AdminRoute from './components/AdminRoute';
import HomePage from './pages/HomePage';
import ShoppingListsPage from './pages/ShoppingListsPage';
import ListPageDetail from './pages/ListPageDetail';
import NotFoundPage from './pages/NotFoundPage';
import PreviousItemNamesPage from './pages/PreviousItemNamesPage';
import RecipesPage from './pages/RecipesPage';
import RecipeDetailPage from './pages/RecipeDetailPage';
import FamiliesPage from './pages/FamiliesPage';
import MealPlannerPage from './pages/MealPlannerPage';
import AdminPage from './pages/AdminPage';
import BillingPage from './pages/BillingPage';
import PrivacyPolicyPage from './pages/PrivacyPolicyPage';
import TermsOfServicePage from './pages/TermsOfServicePage';
import InstallPrompt from './components/InstallPrompt';
import NotificationBanner from './components/NotificationBanner';

function App() {
    const { checkAuthStatus, isLoading, isAuthenticated } = useAuthStore();
    const location = useLocation();

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

      {/* You could potentially add routes here that *don't* use the Layout if needed */}

    </Routes>
    </>
  )
}

export default App
