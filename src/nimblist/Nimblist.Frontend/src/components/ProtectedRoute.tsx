// src/components/ProtectedRoute.tsx
import React from 'react';
import { Outlet } from 'react-router-dom';
import useAuthStore from '../store/authStore';
import LoginPrompt from './LoginPrompt'; // Assume this component exists

const ProtectedRoute: React.FC = () => {
  const { isAuthenticated, isLoading } = useAuthStore();

  if (isLoading) {
    // Show a loading indicator while checking authentication status
    // Important to prevent rendering Outlet/LoginPrompt prematurely
    return <div>Checking authentication...</div>;
  }

  // If authenticated, render the child routes defined within this route in App.tsx
  // If not authenticated, render the LoginPrompt component (or redirect)
  return isAuthenticated ? <Outlet /> : <LoginPrompt />;

  // --- Alternative: Redirect to a frontend login page if you have one ---
  // import { Navigate, useLocation } from 'react-router-dom';
  // const location = useLocation();
  // return isAuthenticated
  //  ? <Outlet />
  //  : <Navigate to="/login" state={{ from: location }} replace />;
  // This requires you to have a <Route path="/login" element={<LoginPage />} /> defined.
  // Given the current flow links to backend login, rendering LoginPrompt might be simpler.
};

export default ProtectedRoute;