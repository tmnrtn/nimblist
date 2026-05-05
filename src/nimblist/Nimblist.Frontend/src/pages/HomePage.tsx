import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import useAuthStore from '../store/authStore';

const HomePage: React.FC = () => {
  const { isAuthenticated } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isAuthenticated) return;
    const lastListId = localStorage.getItem('nimblist_last_list');
    navigate(lastListId ? `/lists/${lastListId}` : '/lists', { replace: true });
  }, [isAuthenticated, navigate]);

  if (isAuthenticated) return null;

  return (
    <div>
      <h2>Home Page</h2>
      <p>Welcome to Nimblist!</p>
    </div>
  );
};

export default HomePage;
