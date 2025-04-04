// src/store/authStore.ts
import { create } from 'zustand';

// Define the shape of the user object you expect from the API
interface User {
  userId: string;
  email: string;
  // Add other properties like name if returned by /api/auth/userinfo
}

export interface AuthState {
  isAuthenticated: boolean;
  user: User | null;
  isLoading: boolean; // Tracks the initial auth check
  checkAuthStatus: () => Promise<void>;
  logout: () => Promise<void>;
  // We don't need an explicit login action here,
  // as login happens via backend redirect. checkAuthStatus verifies the result.
}

const useAuthStore = create<AuthState>((set) => ({
  // Initial State
  isAuthenticated: false,
  user: null,
  isLoading: true, // Start loading until the first check is complete

  // Actions
  checkAuthStatus: async () => {
    // Avoid multiple checks running simultaneously if already loading
    // if (get().isLoading) return; // Optional: uncomment if needed
    set({ isLoading: true });
    try {
      // Make API call to check auth status (browser sends cookie automatically)
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/api/auth/userinfo`, {
         // Add credentials only if necessary (e.g., different subdomains) and CORS allows it.
         // Usually not needed for localhost or same-site deployments. Test first without it.
         credentials: 'include',
         headers: {
            'Accept': 'application/json', // Ensure backend knows this is an API call
         }
      });

      if (response.ok) {
        const userData: User = await response.json();
        set({ isAuthenticated: true, user: userData, isLoading: false });
        console.log("Auth Status: User Authenticated", userData);
      } else if (response.status === 401) { // Explicitly Unauthorized
        set({ isAuthenticated: false, user: null, isLoading: false });
        console.log("Auth Status: User Not Authenticated");
      } else {
        // Handle other errors (e.g., server error on the endpoint)
        console.error(`Auth Status Check Failed: ${response.status} ${response.statusText}`);
        set({ isAuthenticated: false, user: null, isLoading: false });
      }
    } catch (error) {
      // Handle network errors
      console.error("Auth Status Check Network Error:", error);
      set({ isAuthenticated: false, user: null, isLoading: false });
    }
  },

  logout: async () => {
    set({ isLoading: true });
    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/api/auth/logout`, {
        method: 'POST',
        credentials: 'include', // May be needed, same as above
        // Add headers if your POST endpoint requires specific Content-Type or CSRF token
         headers: {
             'Accept': 'application/json',
         }
      });
      if (!response.ok) {
          console.error("Backend logout failed:", response.status);
      }
    } catch (error) {
      console.error("Logout Network Error:", error);
    } finally {
      // Always update client state
      set({ isAuthenticated: false, user: null, isLoading: false });
      console.log("User logged out (client state cleared).");
      // Optional: Force navigation after logout if desired
      // window.location.href = '/'; // Or redirect using react-router programmatically
    }
  },
}));

export default useAuthStore;