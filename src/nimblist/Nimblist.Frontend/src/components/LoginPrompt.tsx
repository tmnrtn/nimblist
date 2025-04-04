// src/components/LoginPrompt.tsx (or similar component)
import React from 'react';

const LoginPrompt: React.FC = () => {
  const backendUrl = import.meta.env.VITE_API_BASE_URL; // Check if this env var is set!
  const loginPageUrl = `${backendUrl}/Identity/Account/Login`;

  // This is the relevant part:
  const currentPathAndQuery = window.location.pathname + window.location.search;
  const returnUrl = encodeURIComponent(currentPathAndQuery);
  const loginUrlWithReturn = `${loginPageUrl}?returnUrl=${returnUrl}`;
  // -----------------------------

  // Log the generated URL for debugging:
  console.log("Generated Login URL:", loginUrlWithReturn);

  return (
    // ... JSX using loginUrlWithReturn in an <a> tag ...
    <a href={loginUrlWithReturn} className="btn btn-primary">
        Login / Register
    </a>
  );
};

export default LoginPrompt;