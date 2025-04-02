import { useEffect } from 'react'; // Add useEffect
import { Routes, Route } from 'react-router-dom';
import './App.css'

import useAuthStore from './store/authStore'; // Import the auth store

import Layout from './components/Layout'; // Your layout component
import ProtectedRoute from './components/ProtectedRoute'; // Import the component
import HomePage from './pages/HomePage';
import ShoppingListsPage from './pages/ShoppingListsPage';
import ListPageDetail from './pages/ListPageDetail';
import NotFoundPage from './pages/NotFoundPage';

function App() {
    // Get state and actions from the store
    const { checkAuthStatus, isLoading } = useAuthStore();

    // Run the auth check once when the component mounts
    useEffect(() => {
      console.log("App component mounted. Checking auth status...");
      checkAuthStatus();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []); // Empty dependency array runs only on mount
  
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
        </Route>

        {/* Catch-all route for any paths not matched above */}
        <Route path="*" element={<NotFoundPage />} />

      </Route> {/* End of routes using the Layout */}

      {/* You could potentially add routes here that *don't* use the Layout if needed */}

    </Routes>
  )
}

export default App
