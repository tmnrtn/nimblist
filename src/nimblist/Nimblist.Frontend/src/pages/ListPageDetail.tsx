// src/pages/ListPageDetail.tsx
import React, { useState, useEffect, useRef, useCallback, FormEvent } from "react";
import { useParams, Link } from "react-router-dom";
import useAuthStore from "../store/authStore";
import { ShoppingList, Item, RecipeSummary } from "../types";
import ItemList from "../components/ItemList";
import useShoppingListHub from "../hooks/useShoppingListHub";
import { authenticatedFetch } from "../components/HttpHelper";
import ItemNameAutocomplete, { type ItemNameAutocompleteHandle } from "../components/ItemNameAutocomplete";
import { usePageTitle } from '../hooks/usePageTitle';

const ListPageDetail: React.FC = () => {
  const { listId } = useParams<{ listId: string }>();
  const { isAuthenticated } = useAuthStore();

  const [list, setList] = useState<ShoppingList | null>(null);
  usePageTitle(list?.name ?? undefined);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // --- State for the 'Add Item' form ---
  const [newItemName, setNewItemName] = useState<string>("");
  const [newItemQuantity, setNewItemQuantity] = useState<string>("");
  const [isAdding, setIsAdding] = useState<boolean>(false); // Loading state for add operation
  const [addError, setAddError] = useState<string | null>(null); // Error specific to adding

  // --- Optimistic UI Error State ---
  const [optimisticError, setOptimisticError] = useState<string | null>(null);

  const itemNameRef = useRef<ItemNameAutocompleteHandle>(null);

  // *** Use the SignalR Hook ***
  // Pass the listId. The hook manages connect/disconnect/join/leave.
  const { connection } = useShoppingListHub(listId);

  const handleItemAdded = useCallback((newItem: Item) => {
    setList((prevList) => {
      if (!prevList || !prevList.items) return prevList;
      if (prevList.items.some((item) => item.id === newItem.id)) return prevList;
      return { ...prevList, items: [...prevList.items, newItem] };
    });
  }, []);

  const handleItemDeleted = useCallback((deletedItemId: string) => {
    setList((prevList) => {
      if (!prevList || !prevList.items) return prevList;
      if (!prevList.items.some((item) => item.id === deletedItemId)) return prevList;
      return { ...prevList, items: prevList.items.filter((item) => item.id !== deletedItemId) };
    });
  }, []);

  const handleItemUpdated = useCallback((updatedItem: Item) => {
    setList((prevList) => {
      if (!prevList || !prevList.items) return prevList;
      const existing = prevList.items.find((item) => item.id === updatedItem.id);
      if (!existing) return prevList;
      if (JSON.stringify(existing) === JSON.stringify(updatedItem)) return prevList;
      return {
        ...prevList,
        items: prevList.items.map((item) => item.id === updatedItem.id ? updatedItem : item),
      };
    });
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
          setList(data);
          localStorage.setItem('nimblist_last_list', listId);
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
    if (!connection) return;
    connection.on("ReceiveItemAdded", handleItemAdded);
    connection.on("ReceiveItemDeleted", handleItemDeleted);
    connection.on("ReceiveItemUpdated", handleItemUpdated);
    return () => {
      connection.off("ReceiveItemAdded", handleItemAdded);
      connection.off("ReceiveItemDeleted", handleItemDeleted);
      connection.off("ReceiveItemUpdated", handleItemUpdated);
    };
  }, [connection, handleItemAdded, handleItemDeleted, handleItemUpdated]);

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
      const response = await authenticatedFetch(`/api/items`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          name: newItemName.trim(),
          quantity: newItemQuantity.trim() || null,
          shoppingListId: listId,
        }),
      });

      const newItemData: Item = await response.json();
      setList((prevList) => {
        if (!prevList) return null;
        if (prevList.items.some((item) => item.id === newItemData.id)) return prevList;
        return { ...prevList, items: [...prevList.items, newItemData] };
      });
      setNewItemName("");
      setNewItemQuantity("");
    } catch (err) {
      console.error("Network error adding item:", err);
      setAddError("Failed to add item. Please try again.");
    } finally {
      setIsAdding(false);
      // Defer focus so it runs after React re-enables the input
      setTimeout(() => itemNameRef.current?.focus(), 0);
    }
  };

  // --- Optimistic Handlers ---
  // Delete item
  const handleDeleteItem = async (itemId: string) => {
    if (!list) return;
    setOptimisticError(null);
    const prevItems = list.items;
    setList({ ...list, items: prevItems.filter((i) => i.id !== itemId) });
    try {
      await authenticatedFetch(`/api/items/${itemId}`, { method: "DELETE" });
    } catch {
      setList((l) => l ? { ...l, items: prevItems } : l);
      setOptimisticError("Failed to delete item. Please try again.");
    }
  };

  // Edit item
  const handleEditItem = async (item: Item, update: { name: string; quantity: string | null; categoryId: string | null; subCategoryId: string | null; isChecked?: boolean; }) => {
    if (!list) return;
    setOptimisticError(null);
    const prevItems = list.items;
    const idx = prevItems.findIndex((i) => i.id === item.id);
    if (idx === -1) return;
    // Use isChecked from update if present, otherwise keep existing
    const updated: Item = {
      ...prevItems[idx],
      ...update,
      isChecked: update.isChecked !== undefined ? update.isChecked : prevItems[idx].isChecked,
      shoppingListId: prevItems[idx].shoppingListId,
    };
    setList({
      ...list,
      items: [
        ...prevItems.slice(0, idx),
        updated,
        ...prevItems.slice(idx + 1),
      ],
    });
    try {
      await authenticatedFetch(`/api/items/${item.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...update,
          isChecked: updated.isChecked,
          shoppingListId: prevItems[idx].shoppingListId,
        }),
      });

      if (update.categoryId && update.categoryId !== item.categoryId) {
        authenticatedFetch("/api/classificationfeedback", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            itemName: update.name,
            categoryId: update.categoryId,
            subCategoryId: update.subCategoryId ?? null,
          }),
        }).catch(() => {});
      }
    } catch {
      setList((l) => (l ? { ...l, items: prevItems } : l));
      setOptimisticError("Failed to update item. Please try again.");
    }
  };

  // Delete all checked
  const [bulkDeleteLoading, setBulkDeleteLoading] = useState(false);

  const handleDeleteAllChecked = async () => {
    if (!list) return;
    setOptimisticError(null);
    setBulkDeleteLoading(true);
    const prevItems = list.items;
    const checkedItems = prevItems.filter((i) => i.isChecked);
    if (checkedItems.length === 0) {
      setBulkDeleteLoading(false);
      return;
    }
    setList({ ...list, items: prevItems.filter((i) => !i.isChecked) });
    try {
      await Promise.all(
        checkedItems.map((item) => authenticatedFetch(`/api/items/${item.id}`, { method: "DELETE" }))
      );
    } catch {
      setList((l) => (l ? { ...l, items: prevItems } : l));
      setOptimisticError("Failed to delete checked items. Please try again.");
    } finally {
      setBulkDeleteLoading(false);
    }
  };

  // Delete all items
  const [deleteAllLoading, setDeleteAllLoading] = useState(false);

  // --- Add from recipe ---
  const [showRecipePanel, setShowRecipePanel] = useState(false);
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [recipesLoaded, setRecipesLoaded] = useState(false);
  const [recipeSearch, setRecipeSearch] = useState('');
  const [selectedRecipeId, setSelectedRecipeId] = useState('');
  const [isAddingFromRecipe, setIsAddingFromRecipe] = useState(false);
  const [addFromRecipeResult, setAddFromRecipeResult] = useState<string | null>(null);
  const [addFromRecipeError, setAddFromRecipeError] = useState<string | null>(null);

  const handleDeleteAll = async () => {
    if (!list || list.items.length === 0) return;
    setOptimisticError(null);
    setDeleteAllLoading(true);
    const prevItems = list.items;
    setList({ ...list, items: [] });
    try {
      await Promise.all(
        prevItems.map((item) => authenticatedFetch(`/api/items/${item.id}`, { method: "DELETE" }))
      );
    } catch {
      setList((l) => (l ? { ...l, items: prevItems } : l));
      setOptimisticError("Failed to delete all items. Please try again.");
    } finally {
      setDeleteAllLoading(false);
    }
  };

  const handleOpenRecipePanel = async () => {
    const opening = !showRecipePanel;
    setShowRecipePanel(opening);
    if (opening && !recipesLoaded) {
      try {
        const r = await authenticatedFetch('/api/recipes');
        const data = r.ok ? await r.json() : [];
        const list: RecipeSummary[] = (Array.isArray(data) ? data : [])
          .sort((a: RecipeSummary, b: RecipeSummary) => a.title.localeCompare(b.title));
        setRecipes(list);
        if (list.length > 0) setSelectedRecipeId(list[0].id);
      } catch { /* ignore */ }
      setRecipesLoaded(true);
    }
  };

  const handleAddFromRecipe = async (e: FormEvent) => {
    e.preventDefault();
    if (!selectedRecipeId || !listId) return;
    setIsAddingFromRecipe(true);
    setAddFromRecipeError(null);
    setAddFromRecipeResult(null);
    try {
      const response = await authenticatedFetch(
        `/api/recipes/${selectedRecipeId}/addtolist/${listId}`,
        { method: 'POST' }
      );
      if (response.ok) {
        const data = await response.json();
        const recipe = recipes.find(r => r.id === selectedRecipeId);
        setAddFromRecipeResult(
          `Added ${data.addedCount} item${data.addedCount !== 1 ? 's' : ''} from "${recipe?.title ?? 'recipe'}"`
        );
        setTimeout(() => setAddFromRecipeResult(null), 4000);
      } else {
        const body = await response.json().catch(() => null);
        setAddFromRecipeError(body?.error ?? `Failed to add ingredients (${response.status})`);
      }
    } catch {
      setAddFromRecipeError('Network error — could not add ingredients.');
    } finally {
      setIsAddingFromRecipe(false);
    }
  };

  // --- Render Logic ---
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
              ref={itemNameRef}
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
            disabled={isAdding || !newItemName.trim()}
            aria-busy={isAdding}
            className="px-4 py-2 bg-blue-600 text-white font-semibold rounded-md shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {isAdding ? "Adding..." : "Add Item"}
          </button>
        </div>
      </form>

      {/* --- Add from Recipe --- */}
      <div className="border border-gray-200 rounded-md overflow-hidden">
        <button
          onClick={handleOpenRecipePanel}
          className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-gray-100 transition-colors text-sm font-medium text-gray-700"
        >
          <span>Add ingredients from a recipe</span>
          <span className="text-gray-400 text-xs">{showRecipePanel ? '▲' : '▼'}</span>
        </button>

        {showRecipePanel && (
          <div className="p-4 bg-white space-y-3">
            {recipesLoaded && recipes.length === 0 && (
              <p className="text-sm text-gray-500">No recipes found. Import or create one on the Recipes page.</p>
            )}
            {!recipesLoaded && (
              <p className="text-sm text-gray-500">Loading recipes…</p>
            )}
            {recipesLoaded && recipes.length > 0 && (() => {
              const q = recipeSearch.trim().toLowerCase();
              const filtered = q ? recipes.filter(r => r.title.toLowerCase().includes(q)) : recipes;
              return (
                <form onSubmit={handleAddFromRecipe} className="space-y-3">
                  {addFromRecipeError && (
                    <p className="text-sm text-red-600 bg-red-50 p-2 rounded border border-red-200">{addFromRecipeError}</p>
                  )}
                  {addFromRecipeResult && (
                    <p className="text-sm text-green-700 bg-green-50 p-2 rounded border border-green-200">{addFromRecipeResult}</p>
                  )}
                  <input
                    type="search"
                    value={recipeSearch}
                    onChange={e => {
                      setRecipeSearch(e.target.value);
                      const q = e.target.value.trim().toLowerCase();
                      const match = q ? recipes.find(r => r.title.toLowerCase().includes(q)) : recipes[0];
                      if (match) setSelectedRecipeId(match.id);
                    }}
                    placeholder="Search recipes…"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500"
                  />
                  <select
                    value={selectedRecipeId}
                    onChange={e => setSelectedRecipeId(e.target.value)}
                    disabled={isAddingFromRecipe || filtered.length === 0}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
                    size={Math.min(filtered.length, 6)}
                  >
                    {filtered.map(r => (
                      <option key={r.id} value={r.id}>{r.title}</option>
                    ))}
                  </select>
                  {filtered.length === 0 && (
                    <p className="text-xs text-gray-400">No recipes match your search.</p>
                  )}
                  <button
                    type="submit"
                    disabled={isAddingFromRecipe || !selectedRecipeId || filtered.length === 0}
                    aria-busy={isAddingFromRecipe}
                    className="px-4 py-2 bg-indigo-600 text-white text-sm font-semibold rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    {isAddingFromRecipe ? 'Adding…' : 'Add ingredients'}
                  </button>
                </form>
              );
            })()}
          </div>
        )}
      </div>

      {/* --- Items List (remains mostly the same) --- */}
      <div className="bg-white shadow overflow-hidden sm:rounded-md">

          <ItemList
        initialItems={list.items ? list.items : []}
        listId={listId!}
        onDeleteItem={(_id) => handleDeleteItem(_id)}
        onEditItem={handleEditItem}
        onDeleteAllChecked={handleDeleteAllChecked}
        onDeleteAll={handleDeleteAll}
        error={optimisticError}
        bulkDeleteLoading={bulkDeleteLoading}
        deleteAllLoading={deleteAllLoading}
      />

      </div>
    </div>
  );
};

export default ListPageDetail;
