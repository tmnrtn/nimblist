// src/pages/ShoppingListsPage.tsx
import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import useAuthStore from '../store/authStore'; // Import your Zustand auth store
import { ShoppingList } from '../types'; // Import the interface (adjust path if needed)

const ShoppingListsPage: React.FC = () => {
  // Get authentication status from the global store
  const { isAuthenticated } = useAuthStore();

  // --- Component State ---
  const [lists, setLists] = useState<ShoppingList[]>([]); // To hold the fetched lists
  const [isLoading, setIsLoading] = useState<boolean>(false); // To show loading indicator
  const [error, setError] = useState<string | null>(null); // To display errors

  // --- Fetching Logic ---
  useEffect(() => {
    // Don't attempt to fetch if the user isn't logged in
    // (Protected route should ideally prevent this, but good defense)
    if (!isAuthenticated) {
      setError("Please log in to view your shopping lists.");
      return;
    }

    const fetchUserLists = async () => {
      setIsLoading(true); // Signal start of fetch
      setError(null);     // Clear any previous errors

      try {
        const apiUrl = `${import.meta.env.VITE_API_BASE_URL}/api/shoppinglists`;
        const response = await fetch(apiUrl, {
          method: 'GET',
          headers: {
            'Accept': 'application/json',
            // No 'Content-Type' needed for GET
          },
          // IMPORTANT: Send credentials (cookies) with the request
          credentials: 'include',
        });

        if (response.ok) { // Status 200-299
          const data: ShoppingList[] = await response.json();
          setLists(data);
        } else if (response.status === 401) { // Unauthorized
          setError("Your session may have expired. Please log out and log back in.");
          // Optionally trigger logout from auth store here: useAuthStore.getState().logout();
        } else { // Other server errors (404, 500, etc.)
          const errorText = await response.text(); // Get error details if available
          console.error(`API Error: ${response.status} ${response.statusText}`, errorText);
          setError(`Failed to load lists. Server responded with ${response.status}.`);
        }
      } catch (err) { // Network errors (fetch failed entirely)
        console.error("Network error fetching lists:", err);
        setError("Unable to connect to the server. Please check your network connection.");
      } finally {
        setIsLoading(false); // Signal end of fetch, regardless of success/failure
      }
    };

    fetchUserLists();

  }, [isAuthenticated]); // Re-fetch if authentication status changes

  // --- Render Logic ---

  // 1. Handle Loading State
  if (isLoading) {
    return <div className="text-center p-6">Loading lists...</div>;
  }

  // 2. Handle Error State
  if (error) {
    return <div className="p-4 my-4 text-sm text-red-700 bg-red-100 rounded-lg border border-red-400" role="alert">{error}</div>;
  }

  // 3. Handle No Lists State (after loading, no error)
  if (!isLoading && lists.length === 0) {
    return (
      <div>
        <h2 className="text-2xl font-semibold mb-4">My Shopping Lists</h2>
        <p className="text-gray-600">You haven't created any lists yet!</p>
        {/* TODO: Add Create New List button/functionality here */}
         <button className="mt-4 bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded transition-colors">
            Create Your First List
         </button>
      </div>
    );
  }

  // 4. Display Lists (loading finished, no errors, lists exist)
  return (
    <div className="space-y-4"> {/* Tailwind class for spacing between children */}
      <div className="flex justify-between items-center">
          <h2 className="text-2xl font-semibold">My Shopping Lists</h2>
          {/* TODO: Add Create New List button/functionality here */}
          <button className="bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded transition-colors">
             Create New List
          </button>
      </div>

      <ul className="bg-white shadow overflow-hidden sm:rounded-md divide-y divide-gray-200">
        {lists.map((list) => (
          <li key={list.id} className="px-4 py-4 sm:px-6 hover:bg-gray-50 transition-colors">
            <div className="flex items-center justify-between">
                <Link
                  to={`/lists/${list.id}`} // Link to the detail page route
                  className="text-lg font-medium text-indigo-600 hover:text-indigo-800 hover:underline truncate"
                  title={list.name} // Tooltip for long names
                >
                  {list.name}
                </Link>
                <div className="ml-2 flex-shrink-0 flex space-x-2">
                   {/* TODO: Add Edit/Delete buttons here */}
                   <span className="text-xs text-gray-500">
                       Created: {new Date(list.createdAt).toLocaleDateString()}
                   </span>
                </div>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default ShoppingListsPage;