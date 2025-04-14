// src/pages/ShoppingListsPage.tsx
import React, { useState, useEffect, FormEvent  } from "react";
import { Link } from "react-router-dom";
import useAuthStore from "../store/authStore"; // Import your Zustand auth store
import { ShoppingList } from "../types"; // Import the interface (adjust path if needed)
import { authenticatedFetch } from "../components/HttpHelper"; // Adjust path as needed

const ShoppingListsPage: React.FC = () => {
  // Get authentication status from the global store
  const { isAuthenticated } = useAuthStore();

  // --- Component State ---
  const [lists, setLists] = useState<ShoppingList[]>([]); // To hold the fetched lists
  const [isLoading, setIsLoading] = useState<boolean>(false); // To show loading indicator
  const [error, setError] = useState<string | null>(null); // To display errors
  const [isAdding, setIsAdding] = useState<boolean>(false); // To show loading indicator
  const [showNew, setShowNew] = useState<boolean>(false); // To show loading indicator
  const [addError, setAddError] = useState<string | null>(null); // Error specific to adding
  const [newListName, setNewListName] = useState<string>("");

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
      setError(null); // Clear any previous errors

      try {
        const apiUrl = `/api/shoppinglists`;
        const response = await authenticatedFetch(apiUrl, {
          method: "GET",
          headers: {
            Accept: "application/json", // Often good practice
          },
        });

        if (response.ok) {
          // Status 200-299
          const data: ShoppingList[] = await response.json();
          setLists(data);
        } else if (response.status === 401) {
          // Unauthorized
          setError(
            "Your session may have expired. Please log out and log back in."
          );
          // Optionally trigger logout from auth store here: useAuthStore.getState().logout();
        } else {
          // Other server errors (404, 500, etc.)
          const errorText = await response.text(); // Get error details if available
          console.error(
            `API Error: ${response.status} ${response.statusText}`,
            errorText
          );
          setError(
            `Failed to load lists. Server responded with ${response.status}.`
          );
        }
      } catch (err) {
        // Network errors (fetch failed entirely)
        console.error("Network error fetching lists:", err);
        setError(
          "Unable to connect to the server. Please check your network connection."
        );
      } finally {
        setIsLoading(false); // Signal end of fetch, regardless of success/failure
      }
    };

    fetchUserLists();
  }, [isAuthenticated]); // Re-fetch if authentication status changes

  const handleClickNew = async () => {
    setShowNew(!showNew); // Toggle the new list form
  };

  const handleAddList = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    setIsAdding(true); // Signal start of adding a list
    setAddError(null); // Clear previous add errors

    try {
      const apiUrl = `/api/shoppingLists`;
      const response = await authenticatedFetch(apiUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ name: newListName }), // Send the new list name
      });

      if (response.ok) {
        // Status 200-299
        const newList: ShoppingList = await response.json();
        setLists((prevLists) => [...prevLists, newList]); // Update state with the new list
        setNewListName(""); // Clear the input field
        setShowNew(false); // Hide the new list form
      } else if (response.status === 400) {
        // Bad Request (e.g., validation error)
        const errorText = await response.text(); // Get error details if available
        console.error(
          `API Error: ${response.status} ${response.statusText}`,
          errorText
        );
        setAddError("Failed to add item. Please check your input.");
      } else {
        // Other server errors (404, 500, etc.)
        const errorText = await response.text(); // Get error details if available
        console.error(
          `API Error: ${response.status} ${response.statusText}`,
          errorText
        );
        setAddError("Failed to add item. Please try again later.");
      }
    }
     catch (err) {
      // Handle network errors
      console.error("Network error adding item:", err);
      setAddError("Failed to connect to the server to add item.");
    } finally {
      setIsAdding(false); // Signal end of adding, regardless of success/failure
    }
  };

  // --- Render Logic ---

  // 1. Handle Loading State
  if (isLoading) {
    return <div className="text-center p-6">Loading lists...</div>;
  }

  // 2. Handle Error State
  if (error) {
    return (
      <div
        className="p-4 my-4 text-sm text-red-700 bg-red-100 rounded-lg border border-red-400"
        role="alert"
      >
        {error}
      </div>
    );
  }

  // 4. Display Lists (loading finished, no errors, lists exist)
  return (
    <div className="space-y-4">
      {" "}
      {/* Tailwind class for spacing between children */}
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-semibold">My Shopping Lists</h2>
        {/* TODO: Add Create New List button/functionality here */}
        <button
          className="bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded transition-colors"
          onClick={handleClickNew}
        >
          Create New List
        </button>
      </div>
      {showNew && (
        <div className="text-sm text-gray-500 mb-2">
          <form
            onSubmit={handleAddList}
            className="p-4 bg-gray-100 rounded-md shadow space-y-3 border border-gray-200"
          >
            <h3 className="text-lg font-medium text-gray-700">Add New List</h3>
            {/* Display any error specific to adding */}
            {addError && (
              <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">
                {addError}
              </p>
            )}
            <div className="flex flex-col sm:flex-row sm:space-x-2 space-y-2 sm:space-y-0">
              <input
                type="text"
                value={newListName}
                onChange={(e) => setNewListName(e.target.value)}
                placeholder="List Name (required)"
                aria-label="New list name"
                required // HTML5 validation
                className="flex-grow px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm disabled:bg-gray-200"
                disabled={isAdding} // Disable input while submitting
              />
              <button
                type="submit"
                disabled={isAdding || !newListName.trim()} // Disable if adding or name is empty
                className="px-4 py-2 bg-blue-600 text-white font-semibold rounded-md shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isAdding ? "Adding..." : "Add Item"}
              </button>
            </div>
          </form>
        </div>
      )}
      <ul className="bg-white shadow overflow-hidden sm:rounded-md divide-y divide-gray-200">
        {lists.map((list) => (
          <li
            key={list.id}
            className="px-4 py-4 sm:px-6 hover:bg-gray-50 transition-colors"
          >
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
