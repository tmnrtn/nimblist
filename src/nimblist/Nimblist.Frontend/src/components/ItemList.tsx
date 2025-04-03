// src/components/ItemList.tsx
import React, { useState, useEffect } from "react";
import { Item } from "../types"; // Adjust path as needed
import clsx from 'clsx';
import {authenticatedFetch} from "../components/HttpHelper"; // Adjust path as needed

// --- Props Interface ---
interface ItemListProps {
  initialItems: Item[];
  listId: string; // Assuming listId is passed down for context/API calls if needed
  // Optional callback if parent needs notification of changes
  // onItemToggled?: (itemId: string, newState: boolean) => void;
}


// --- End Helper ---

const ItemList: React.FC<ItemListProps> = ({ initialItems }) => {
  // --- State ---
  const [items, setItems] = useState<Item[]>(initialItems);
  const [error, setError] = useState<string | null>(null);
  const [loadingItemId, setLoadingItemId] = useState<string | null>(null);

  // Update local state if the initialItems prop changes from parent
  useEffect(() => {
    setItems(initialItems);
  }, [initialItems]);

  // --- Toggle Handler ---
  const handleToggleCheck = async (itemId: string): Promise<void> => {
    if (loadingItemId === itemId) return;

    setLoadingItemId(itemId);
    setError(null);

    const originalItem = items.find(i => i.id === itemId);
    if (!originalItem) {
        setLoadingItemId(null);
        console.warn(`Item with id ${itemId} not found in current state.`);
        return;
    }

    // Determine the new state and create the updated item for UI and API
    const newCheckedState = !originalItem.isChecked;
    const updatedItem = { ...originalItem, isChecked: newCheckedState };

    // ** Optimistic UI Update **
    setItems(currentItems =>
        currentItems.map(item =>
            item.id === itemId ? updatedItem : item // Use the fully updated item object
        )
    );

    // --- API Call ---
    try {
        // Use the standard PUT endpoint for the item resource
        const apiUrl = `/api/items/${itemId}`;

        // Prepare the payload - send the updated item object
        // Ensure this matches what your backend PUT endpoint expects!
        // It might expect the full item or just certain fields.
        // Using the 'updatedItem' assumes PUT replaces/updates based on this representation.
        const payload = updatedItem;

        await authenticatedFetch(apiUrl, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json', // Often good practice
            },
            body: JSON.stringify(payload) // Send the updated item data
        });

        console.log(`Item ${itemId} updated successfully on backend.`);
        // Optional: Notify parent if needed
        // onItemToggled?.(itemId, newCheckedState);

    } catch (err) {
        // ** Rollback UI on Error **
        setError(`Failed to update item '${originalItem.name}'. Please refresh or try again.`);
        console.error("Update item API failed:", err);
        // Revert UI state back to the original item state
        setItems(currentItems =>
            currentItems.map(item =>
                item.id === itemId ? originalItem : item
            )
        );
    } finally {
        // --- Clear Loading State ---
        setLoadingItemId(null);
    }
};

  // --- Delete Handler ---
  const handleDeleteItem = async (itemIdToDelete: string, itemName: string): Promise<void> => {
    // Prevent running if another operation is in progress for this item
    if (loadingItemId === itemIdToDelete) return;

    // ** Confirmation Dialog ** (Good Practice)
    if (!window.confirm(`Are you sure you want to delete "${itemName}"?`)) {
      return; // Stop if user cancels
    }

    setLoadingItemId(itemIdToDelete); // Set loading state for feedback/disabling buttons
    setError(null);

    // --- API Call ---
    try {
      const apiUrl = `${import.meta.env.VITE_API_BASE_URL}/api/items/${itemIdToDelete}`;
      await authenticatedFetch(apiUrl, { method: 'DELETE' });

      // ** Update UI State on Success **
      // Filter out the deleted item from the local state *after* successful deletion
      setItems(currentItems => currentItems.filter(item => item.id !== itemIdToDelete));

      console.log(`Item ${itemIdToDelete} deleted successfully.`);

    } catch (err) {
      setError(`Failed to delete item '${itemName}'. Please try again.`);
      console.error("Delete item API failed:", err);
      // No UI rollback needed here because we update state *after* success
    } finally {
      // --- Clear Loading State ---
      setLoadingItemId(null);
    }
  };

  // --- Render Logic with Tailwind ---
  return (
    <div className="bg-white shadow overflow-hidden sm:rounded-md"> {/* Added container styles */}
      <h3 className="text-lg font-medium leading-6 text-gray-900 px-4 py-3 sm:px-6 border-b border-gray-200">
         Shopping Items
         <span className="ml-2 text-sm font-normal text-gray-500">({items.length})</span>
      </h3>
      {error && (
        <div className="p-4 border-b border-gray-200">
            <p className="text-sm text-red-700 bg-red-100 p-3 rounded border border-red-300">{error}</p>
        </div>
      )}
      {items.length === 0 && !error && (
          <p className="px-4 py-4 text-sm text-gray-500 sm:px-6 italic">This list is empty.</p>
      )}
      <ul className="divide-y divide-gray-200"> {/* Use Tailwind's divide for borders */}
        {items.map((item: Item) => (
          <li
            key={item.id}
            // Use clsx library for cleaner conditional classes, or template literals
            className={clsx(
                "px-4 py-3 sm:px-6 flex items-center justify-between hover:bg-gray-50 transition-opacity duration-150",
                loadingItemId === item.id ? 'opacity-50' : 'opacity-100' // Apply opacity when loading
            )}
          >
            {/* Item Checkbox and Name/Quantity */}
            {/* Use group utility if needed for hover states on children */}
            <label 
              htmlFor={"checkbox_" + item.id} // Use a unique id for accessibility
              className={clsx(
                "flex-grow mr-4 flex items-center",
                loadingItemId === item.id ? 'cursor-not-allowed' : 'cursor-pointer'
            )}>
              <input
                id={"checkbox_" + item.id}
                type="checkbox"
                checked={item.isChecked}
                onChange={() => handleToggleCheck(item.id)}
                disabled={loadingItemId === item.id}
                // Use Tailwind form plugin classes if installed, or basic styling
                className={clsx(
                    "h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500 cursor-inherit",
                    // Or using @tailwindcss/forms plugin: 'form-checkbox'
                )}
              />
              {/* Apply conditional text styles */}
              <span className={clsx(
                  "ml-3 text-sm font-medium text-gray-900", // Base text style
                  item.isChecked && 'line-through text-gray-500' // Styles when checked
              )}>
                {item.name}
              </span>
              {item.quantity && (
                <span className="ml-2 text-sm text-gray-500">({item.quantity})</span>
              )}
            </label>

            {/* Delete Button */}
            <button
              onClick={() => handleDeleteItem(item.id, item.name)}
              disabled={loadingItemId === item.id}
              title={`Delete item "${item.name}"`}
              // Tailwind classes for styling the delete button
              className={clsx(
                  "ml-4 p-1 text-sm font-medium text-red-600 hover:text-red-900 hover:bg-red-100 rounded-md",
                  "focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-red-500",
                  "disabled:opacity-50 disabled:cursor-not-allowed transition-all"
              )}
            >
               {/* Recommended: Replace text with an icon */}
               {/* <FaTrash aria-hidden="true" className="h-4 w-4" /> */}
               <span className="sr-only">Delete {item.name}</span> {/* For screen readers if using only icon */}
               Delete {/* Or use icon */}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default ItemList;
