import React from 'react';
import { Link } from 'react-router-dom';
import { usePageTitle } from '../hooks/usePageTitle';

const NotFoundPage: React.FC = () => {
  usePageTitle('Page Not Found');
  return (
    <div className="flex flex-col items-center justify-center py-24 px-4 text-center">
      <p className="text-6xl font-bold text-indigo-600 mb-2">404</p>
      <h1 className="text-2xl font-semibold text-gray-800 mb-2">Page not found</h1>
      <p className="text-gray-500 mb-8 max-w-xs">
        The page you're looking for doesn't exist or may have been moved.
      </p>
      <Link
        to="/"
        className="px-5 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 transition-colors"
      >
        Back to home
      </Link>
    </div>
  );
};

export default NotFoundPage;
