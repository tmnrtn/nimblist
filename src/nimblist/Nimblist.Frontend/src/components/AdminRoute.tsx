import React from 'react';
import { Outlet } from 'react-router-dom';
import useAuthStore from '../store/authStore';

const AdminRoute: React.FC = () => {
  const { isAuthenticated, isAdmin, isLoading } = useAuthStore();

  if (isLoading) return <div>Checking authentication...</div>;
  if (!isAuthenticated) return <div className="p-8 text-center text-gray-500">Please log in to access this page.</div>;
  if (!isAdmin) return <div className="p-8 text-center text-red-600">Access denied. Admin privileges required.</div>;

  return <Outlet />;
};

export default AdminRoute;
