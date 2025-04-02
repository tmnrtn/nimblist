// src/components/Layout.tsx
import React from 'react';
import { Outlet, Link } from 'react-router-dom';
import useAuthStore from '../store/authStore'; // Import the store

const Layout: React.FC = () => {
  // Get state and actions from the store
  const { isAuthenticated, user, logout } = useAuthStore();
  const backendUrl = import.meta.env.VITE_API_BASE_URL; // Get backend base URL

  return (
    <div className="min-h-screen flex flex-col bg-gray-50">
      <header className="bg-gradient-to-r from-blue-600 to-indigo-700 text-white shadow-md">
        <div className="container mx-auto px-4 py-3 flex justify-between items-center">
          <h1 className="text-2xl font-bold tracking-tight">
            <Link to="/" className="hover:text-gray-200 transition-colors">Nimblist</Link>
          </h1>
          <nav>
            <ul className="flex items-center space-x-6">
              <li><Link to="/" className="text-sm font-medium hover:text-gray-200 transition-colors">Home</Link></li>
              {/* Show "My Lists" only if authenticated */}
              {isAuthenticated && (
                <li><Link to="/lists" className="text-sm font-medium hover:text-gray-200 transition-colors">My Lists</Link></li>
              )}
              <li>
                {/* Show User Info & Logout OR Login Link */}
                {isAuthenticated && user ? (
                  <div className="flex items-center space-x-3">
                    <span className="text-sm text-gray-300">({user.email})</span>
                    <button
                      onClick={logout}
                      className="text-sm font-medium bg-red-500 hover:bg-red-600 px-3 py-1 rounded transition-colors"
                    >
                      Logout
                    </button>
                  </div>
                ) : (
                  <a
                    href={`${backendUrl}/Identity/Account/Login?returnUrl=${encodeURIComponent(window.location.pathname)}`}
                    className="text-sm font-medium bg-green-500 hover:bg-green-600 px-3 py-1 rounded transition-colors"
                  >
                    Login / Register
                  </a>
                )}
              </li>
            </ul>
          </nav>
        </div>
      </header>

      <main className="flex-grow container mx-auto px-4 py-6">
        <Outlet />
      </main>

      <footer className="bg-gray-200 text-gray-600 text-center text-sm py-4 mt-8">
        <p>&copy; {new Date().getFullYear()} Nimblist</p>
      </footer>
    </div>
  );
};

export default Layout;