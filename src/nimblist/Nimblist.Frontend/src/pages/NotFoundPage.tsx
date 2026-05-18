import React from 'react';
import { usePageTitle } from '../hooks/usePageTitle';

const NotFoundPage: React.FC = () => {
  usePageTitle('Page Not Found');
  return (
    <div>
      <h2>404 - Page Not Found</h2>
      <p>Sorry, the page you were looking for could not be found.</p>
    </div>
  );
};

export default NotFoundPage;