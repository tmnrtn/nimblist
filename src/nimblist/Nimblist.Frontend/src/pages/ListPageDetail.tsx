// src/pages/ListPageDetail.tsx
import React, { useState, useEffect, FormEvent } from "react"; // Import FormEvent
import { useParams, Link } from "react-router-dom";
import useAuthStore from "../store/authStore";
import { ShoppingList, Item } from "../types";
import ItemList from "../components/ItemList";
import useShoppingListHub from "../hooks/useShoppingListHub"; // <-- Import the hook
import { authenticatedFetch } from "../components/HttpHelper"; // Adjust path as needed
import ItemNameAutocomplete from "../components/ItemNameAutocomplete";

const ListPageDetail: React.FC = () => {
  const { listId } = useParams<{ listId: string }>();
  const { isAuthenticated } = useAuthStore();

  const [list, setList] = useState<ShoppingList | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // --- State for the 'Add Item' form ---
  const [newItemName, setNewItemName] = useState<string>("");
  const [newItemQuantity, setNewItemQuantity] = useState<string>("");
  const [isAdding, setIsAdding] = useState<boolean>(false); // Loading state for add operation
  const [addError, setAddError] = useState<string | null>(null); // Error specific to adding

  // *** Use the SignalR Hook ***
  // Pass the listId. The hook manages connect/disconnect/join/leave.
  const { connection, isConnected: isSignalRConnected } =
    useShoppingListHub(listId);

  useEffect(() => {
    console.log("ListPageDetail MOUNTED with listId:", listId);
    return () => {
      console.log("ListPageDetail UNMOUNTING with listId:", listId);
    };
  }, []);

  // --- Fetching Logic (useEffect for getting list details - as before) ---
  useEffect(() => {
    if (!listId || !isAuthenticated) {
      if (!isAuthenticated) setError("Please log in to view this list.");
      else if (!listId) setError("No List ID provided.");
      setIsLoading(false);
      return;
    }

    const fetchListDetails = async () => {
      // Reset component state on listId change before fetching
      setIsLoading(true);
      setError(null);
      setList(null); // Clear previous list data

      try {
        const apiUrl = `/api/shoppinglists/${listId}`;
        const response = await authenticatedFetch(apiUrl, {
          method: "GET",
          headers: {
            Accept: "application/json", // Often good practice
          },
        });

        if (response.ok) {
          const data: ShoppingList = await response.json();
          //console.log("ListPageDetail: Fetched list details, about to setList. New items count:", data.items.length);
          setList(data);
        } else if (response.status === 401) {
          setError("Authentication error. Please log in again.");
        } else if (response.status === 404) {
          setError("Shopping list not found or you don't have permission.");
        } else {
          setError(`Failed to load list details. Status: ${response.status}`);
        }
      } catch (err) {
        setError("Network error fetching list details.");
        console.error("Fetch error:", err);
      } finally {
        setIsLoading(false);
      }
    };
    fetchListDetails();
  }, [listId, isAuthenticated]);

  // *** Effect for Attaching/Detaching SignalR Message Handlers ***
  useEffect(() => {
    // Only attach handlers if the connection object exists
    if (connection) {
      console.log("ListPageDetail: Attaching SignalR message handlers...");

      // Handler for when a new item is added by *another* user
      const handleItemAdded = (newItem: Item) => {
        setList((prevList) => {
          if (!prevList || !prevList.items) return prevList;
          // Avoid adding duplicate if this client was the one who added it
          if (prevList.items.some((item) => item.id === newItem.id)) {
            return prevList;
          }
          return { ...prevList, items: [...prevList.items, newItem] };
        });
      };

      // Handler for when an item is deleted by *another* user
      const handleItemDeleted = (deletedItemId: string) => {
        setList((prevList) => {
          if (!prevList || !prevList.items) return prevList;
          // Only update if the item actually exists
          if (!prevList.items.some((item) => item.id === deletedItemId)) {
            return prevList;
          }
          return {
            ...prevList,
            items: prevList.items.filter((item) => item.id !== deletedItemId),
          };
        });
      };

      // Handler for when an item is updated (e.g., name/quantity change via PUT)
      const handleItemUpdated = (updatedItem: Item) => {
        console.log("ListPageDetail: SignalR handleItemAdded. Current list items:", list?.items?.length, "New item:", updatedItem.id);
        setList((prevList) => {
          if (!prevList || !prevList.items) return prevList;
          const existing = prevList.items.find(
            (item) => item.id === updatedItem.id
          );
          if (!existing) return prevList;
          // Only update if the item is actually different
          if (JSON.stringify(existing) === JSON.stringify(updatedItem))
            return prevList;
          return {
            ...prevList,
            items: prevList.items.map((item) =>
              item.id === updatedItem.id ? updatedItem : item
            ),
          };
        });
      };

      // Register the handlers with the connection
      connection.on("ReceiveItemAdded", handleItemAdded);
      connection.on("ReceiveItemDeleted", handleItemDeleted);
      connection.on("ReceiveItemUpdated", handleItemUpdated); // Ensure backend sends this

      // Cleanup function for *this* effect: Remove handlers when connection changes or component unmounts
      return () => {
        console.log("ListPageDetail: Removing SignalR message handlers...");
        connection.off("ReceiveItemAdded", handleItemAdded);
        connection.off("ReceiveItemDeleted", handleItemDeleted);
        connection.off("ReceiveItemUpdated", handleItemUpdated);
      };
    }
  }, [connection]); // Dependency: This effect runs when the 'connection' object instance changes

  // --- Handle Add Item Form Submission ---
  const handleAddItem = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); // Prevent default browser form submission behavior

    // Basic client-side validation
    if (!newItemName.trim()) {
      setAddError("Item name is required.");
      return;
    }
    // Ensure listId is available (should be if the component rendered list details)
    if (!listId) {
      setAddError("Cannot add item: List ID is missing.");
      return;
    }
    // Ensure user is still authenticated (check client state)
    if (!isAuthenticated) {
      setAddError("You must be logged in to add items.");
      return;
    }

    setIsAdding(true); // Set loading state for button
    setAddError(null); // Clear previous add errors

    try {
      const apiUrl = `${import.meta.env.VITE_API_BASE_URL}/api/items`; // Assuming POST /api/items endpoint
      const response = await fetch(apiUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        credentials: "include", // Send auth cookie
        body: JSON.stringify({
          name: newItemName.trim(),
          quantity: newItemQuantity.trim() || null, // Send null if quantity is empty/whitespace
          shoppingListId: listId, // Send the ID of the current list
        }),
      });

      if (response.ok) {
        // Check for 201 Created or other 2xx success
        const newItemData: Item = await response.json();

        // --- Update UI State ---
        // Add the new item to the existing list's items array
        setList((prevList) => {
          if (!prevList) return null; // Safety check
          // Create a new list object with the new item appended
          if (prevList.items.some((item) => item.id === newItemData.id)) {
            return prevList;
          }
          return {
            ...prevList,
            items: [...prevList.items, newItemData],
          };
        });

        // --- Clear Form ---
        setNewItemName("");
        setNewItemQuantity("");
      } else {
        // Handle API errors (400 Bad Request, 401, 404, 500 etc.)
        let errorMessage = `Failed to add item. Status: ${response.status}`;
        try {
          const errorData = await response.json();
          // Use specific error message from backend if available
          errorMessage = errorData?.message || errorData?.title || errorMessage;
        } catch {
          /* Ignore if response body isn't JSON */
        }
        setAddError(errorMessage);
        console.error("Add item error response:", response);
      }
    } catch (err) {
      // Handle network errors
      console.error("Network error adding item:", err);
      setAddError("Failed to connect to the server to add item.");
    } finally {
      setIsAdding(false); // Clear loading state for button
    }
  };


  // --- Render Logic ---
  // Handle Error State
  if (error) {
    return (
      <div>
        <Link to="/lists" className="text-sm text-blue-600 hover:underline">
          &larr; Back to Lists
        </Link>
        <div
          className="mt-4 p-4 text-sm text-red-700 bg-red-100 rounded-lg border border-red-400"
          role="alert"
        >
          Error: {error}
        </div>
      </div>
    );
  }

  // Handle List Not Found (after loading, no error, but list still null)
  if (!list) {
    return (
      <div>
        <Link to="/lists" className="text-sm text-blue-600 hover:underline">
          &larr; Back to Lists
        </Link>
        <p className="mt-4 text-gray-600">Could not load list data.</p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div>
        <Link to="/lists" className="text-sm text-blue-600 hover:underline">
          &larr; Back to Lists
        </Link>
        <p className="mt-4 text-gray-600">Loading list data.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* ... Back Link ... */}
      {/* ... List Header ... */}

      {/* --- Add New Item Form --- */}
      <form
        onSubmit={handleAddItem}
        className="p-4 bg-gray-100 rounded-md shadow space-y-3 border border-gray-200"
      >
        <h3 className="text-lg font-medium text-gray-700">Add New Item</h3>
        {/* Display any error specific to adding */}
        {addError && (
          <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">
            {addError}
          </p>
        )}
        <div className="flex flex-col sm:flex-row sm:space-x-2 space-y-2 sm:space-y-0">
          <div className="flex-grow">
            <ItemNameAutocomplete
              value={newItemName}
              onChange={setNewItemName}
              disabled={isAdding}
              onKeyDown={(e: React.KeyboardEvent) => {
                if (e.key === "Enter" && newItemName.trim()) {
                  // Prevent AsyncCreatableSelect from handling Enter
                  e.preventDefault();
                  // Find the form element and submit
                  const form = (e.target as HTMLElement).closest("form");
                  if (form) {
                    // Create a synthetic submit event
                    const submitEvent = new Event("submit", {
                      bubbles: true,
                      cancelable: true,
                    });
                    form.dispatchEvent(submitEvent);
                  }
                }
              }}
            />
          </div>
          <input
            type="text"
            value={newItemQuantity}
            onChange={(e) => setNewItemQuantity(e.target.value)}
            placeholder="Quantity (e.g., 2 packs)"
            aria-label="New item quantity"
            className="sm:w-1/3 px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm disabled:bg-gray-200"
            disabled={isAdding}
          />
          <button
            type="submit"
            disabled={isAdding || !newItemName.trim()} // Disable if adding or name is empty
            className="px-4 py-2 bg-blue-600 text-white font-semibold rounded-md shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {isAdding ? "Adding..." : "Add Item"}
          </button>
        </div>
        <p>
          Real-time status: {isSignalRConnected ? "Connected" : "Disconnected"}
        </p>
      </form>

      {/* --- Items List (remains mostly the same) --- */}
      <div className="bg-white shadow overflow-hidden sm:rounded-md">

          <ItemList initialItems={list.items ? list.items : []} listId={listId!} />

      </div>
    </div>
  );
};

export default ListPageDetail;
