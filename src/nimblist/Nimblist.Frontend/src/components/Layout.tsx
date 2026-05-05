import React, { useState } from 'react';
import { Outlet, Link } from 'react-router-dom';
import useAuthStore from '../store/authStore';

const Layout: React.FC = () => {
  const { isAuthenticated, user, logout } = useAuthStore();
  const backendUrl = import.meta.env.VITE_API_BASE_URL;
  const [menuOpen, setMenuOpen] = useState(false);
  const closeMenu = () => setMenuOpen(false);

  return (
    <div className="min-h-screen flex flex-col bg-gray-50">
      <header className="bg-gradient-to-r from-blue-600 to-indigo-700 text-white shadow-md">
        <div className="container mx-auto px-4 py-3">
          <div className="flex justify-between items-center">
            <h1 className="text-2xl font-bold tracking-tight">
              <Link to="/" className="hover:text-gray-200 transition-colors" onClick={closeMenu}>Nimblist</Link>
            </h1>

            {/* Desktop nav */}
            <nav className="hidden md:block">
              <ul className="flex items-center space-x-6">
                {isAuthenticated && (
                  <>
                    <li><Link to="/lists" className="text-sm font-medium hover:text-gray-200 transition-colors">My Lists</Link></li>
                    <li><Link to="/recipes" className="text-sm font-medium hover:text-gray-200 transition-colors">Recipes</Link></li>
                    <li><Link to="/families" className="text-sm font-medium hover:text-gray-200 transition-colors">Families</Link></li>
                    <li><Link to="/meal-planner" className="text-sm font-medium hover:text-gray-200 transition-colors">Meal Planner</Link></li>
                  </>
                )}
                <li>
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

            {/* Mobile hamburger */}
            <button
              className="md:hidden p-2 rounded hover:bg-white/10 transition-colors"
              onClick={() => setMenuOpen(v => !v)}
              aria-label="Toggle menu"
              aria-expanded={menuOpen}
            >
              <span className="block w-5 h-0.5 bg-white mb-1" />
              <span className="block w-5 h-0.5 bg-white mb-1" />
              <span className="block w-5 h-0.5 bg-white" />
            </button>
          </div>

          {/* Mobile menu */}
          {menuOpen && (
            <nav className="md:hidden border-t border-white/20 mt-3 pt-3 pb-1">
              <ul className="flex flex-col space-y-1">
                {isAuthenticated && (
                  <>
                    <li><Link to="/lists" onClick={closeMenu} className="block py-2 text-sm font-medium hover:text-gray-200 transition-colors">My Lists</Link></li>
                    <li><Link to="/recipes" onClick={closeMenu} className="block py-2 text-sm font-medium hover:text-gray-200 transition-colors">Recipes</Link></li>
                    <li><Link to="/families" onClick={closeMenu} className="block py-2 text-sm font-medium hover:text-gray-200 transition-colors">Families</Link></li>
                    <li><Link to="/meal-planner" onClick={closeMenu} className="block py-2 text-sm font-medium hover:text-gray-200 transition-colors">Meal Planner</Link></li>
                  </>
                )}
                <li className="pt-1">
                  {isAuthenticated && user ? (
                    <div className="flex items-center justify-between py-2">
                      <span className="text-sm text-gray-300 truncate mr-3">{user.email}</span>
                      <button
                        onClick={() => { logout(); closeMenu(); }}
                        className="text-sm font-medium bg-red-500 hover:bg-red-600 px-3 py-1 rounded transition-colors flex-shrink-0"
                      >
                        Logout
                      </button>
                    </div>
                  ) : (
                    <a
                      href={`${backendUrl}/Identity/Account/Login?returnUrl=${encodeURIComponent(window.location.pathname)}`}
                      onClick={closeMenu}
                      className="block py-2 text-sm font-medium bg-green-500 hover:bg-green-600 px-3 py-1 rounded transition-colors"
                    >
                      Login / Register
                    </a>
                  )}
                </li>
              </ul>
            </nav>
          )}
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