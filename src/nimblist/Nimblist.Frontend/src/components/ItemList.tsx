// src/components/ItemList.tsx
import React, { useState, useEffect } from "react";
import { Item } from "../types"; // Adjust path as needed
import clsx from "clsx";
import { authenticatedFetch } from "../components/HttpHelper"; // Adjust path as needed
import Select from "react-select";
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
  type Row,
  type Cell,
  getSortedRowModel,
} from "@tanstack/react-table";

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
  const [error, setError] = useState<string | null>(null);
  const [loadingItemId, setLoadingItemId] = useState<string | null>(null);
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [editName, setEditName] = useState<string>("");
  const [editQuantity, setEditQuantity] = useState<string>("");
  const [editError, setEditError] = useState<string | null>(null);

  // Add types for category and subcategory options
  interface CategoryOption {
    value: string;
    label: string;
  }

  interface SubcategoryOption {
    value: string;
    label: string;
  }

  const noneCategoryOption = React.useMemo(
    () => ({ value: "", label: "(None)" }),
    []
  );
  const noneSubcategoryOption = React.useMemo(
    () => ({ value: "", label: "(None)" }),
    []
  );
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [subcategories, setSubcategories] = useState<SubcategoryOption[]>([]);
  const [selectedCategory, setSelectedCategory] =
    useState<CategoryOption | null>(null);
  const [selectedSubcategory, setSelectedSubcategory] =
    useState<SubcategoryOption | null>(null);
  const [categoryLoading, setCategoryLoading] = useState(false);
  const [subcategoryLoading, setSubcategoryLoading] = useState(false);
  const [sorting, setSorting] = useState<
    import("@tanstack/react-table").SortingState
  >([]);
  const [checkedFilter, setCheckedFilter] = useState<string>("unchecked"); // 'all', 'checked', 'unchecked'

  const filteredItems = React.useMemo(() => {
    if (checkedFilter === "all" || !initialItems) {
      return initialItems;
    }
    const isCheckedValue = checkedFilter === "checked";
    return initialItems.filter((item) => item.isChecked === isCheckedValue);
  }, [initialItems, checkedFilter]);

  const prevInitialItemsRef = React.useRef<Item[] | undefined>(undefined);
  const renderCountRef = React.useRef(0);

  React.useEffect(() => {
    renderCountRef.current += 1;
    console.log(
      `ItemList RENDERED: ${renderCountRef.current} times. initialItems length: ${initialItems.length}`
    );
    if (
      prevInitialItemsRef.current &&
      prevInitialItemsRef.current !== initialItems
    ) {
      // This indicates a new array reference from the parent
      console.warn("ItemList: initialItems prop instance CHANGED!");
    }
    prevInitialItemsRef.current = initialItems;
  }, [initialItems]);

  // Fetch categories on mount or when editing starts
  useEffect(() => {
    console.log("ItemList: Categories effect - STARTING FETCH");
    setCategoryLoading(true);
    authenticatedFetch("/api/categories", {
      method: "GET",
      headers: { Accept: "application/json" },
    })
      .then((res) => {
        if (!res.ok) throw new Error("Failed to fetch categories");
        return res.json();
      })
      .then((data: { id: string; name: string }[]) => {
        setCategories(
          data.map((cat) => ({ value: cat.id, label: cat.name }))
        );
        setCategoryLoading(false);
      })
      .catch((err) => {
        console.error("ItemList: Category fetch error", err);
        //setCategoryLoading(false);
        //setCategories([]);
        setError("Failed to load categories");
        console.error("Category fetch error:", err);
      })
      .finally(() => {
        console.log(
          "ItemList: Categories effect - FETCH COMPLETE, setLoading=false"
        );
        setCategoryLoading(false);
      });
  }, []);

  // Fetch subcategories when a category is selected
  useEffect(() => {
    if (selectedCategory) {
      console.log(
        "ItemList: Subcategories effect - STARTING FETCH for category:",
        selectedCategory.value
      );
      setSubcategoryLoading(true);
      authenticatedFetch(
        `/api/subcategories?parentCategoryId=${selectedCategory.value}`,
        {
          method: "GET",
          headers: { Accept: "application/json" },
        }
      )
        .then((res) => {
          if (!res.ok) throw new Error("Failed to fetch subcategories");
          return res.json();
        })
        .then((data: { id: string; name: string }[]) => {
          setSubcategories(
            data.map((sub) => ({ value: sub.id, label: sub.name }))
          );
          setSubcategoryLoading(false);
        })
        .catch((err) => {
          setSubcategoryLoading(false);
          setSubcategories([]);
          setError("Failed to load subcategories");
          console.error("Subcategory fetch error:", err);
        });
    } else {
      console.log(
        "ItemList: Subcategories effect - no selected category, clearing subcategories."
      );
      //setSubcategories([]);
    }
  }, [selectedCategory]);

  // --- Toggle Handler ---
  const handleToggleCheck = async (itemId: string): Promise<void> => {
    console.log("[ItemList] handleToggleCheck called", itemId);
    if (loadingItemId === itemId) return;

    setLoadingItemId(itemId);
    setError(null);

    const originalItem = initialItems.find((i) => i.id === itemId);
    if (!originalItem) {
      setLoadingItemId(null);
      console.warn(`Item with id ${itemId} not found in current state.`);
      return;
    }

    // Determine the new state and create the updated item for UI and API
    const newCheckedState = !originalItem.isChecked;
    const updatedItem = { ...originalItem, isChecked: newCheckedState };

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
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json" // Often good practice
        },
        body: JSON.stringify(payload) // Send the updated item data
      });

      console.log(`Item ${itemId} updated successfully on backend.`);
      // Optional: Notify parent if needed
      // onItemToggled?.(itemId, newCheckedState);
    } catch (err) {
      // ** Rollback UI on Error **
      setError(
        `Failed to update item '${originalItem.name}'. Please refresh or try again.`
      );
      console.error("Update item API failed:", err);
      // Revert UI state back to the original item state
      // Filter and map to restore the original array state
      // const revertedItems = initialItems.map((item) => (item.id === itemId ? originalItem : item));
    } finally {
      // --- Clear Loading State ---
      setLoadingItemId(null);
    }
  };

  // --- Delete Handler ---
  const handleDeleteItem = async (
    itemIdToDelete: string,
    itemName: string
  ): Promise<void> => {
    console.log("[ItemList] handleDeleteItem called", itemIdToDelete);
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
      const apiUrl = `/api/items/${itemIdToDelete}`;
      await authenticatedFetch(apiUrl, { method: "DELETE" });

      // ** Update UI State on Success **
      // Filter out the deleted item from the local state *after* successful deletion
      // This creates a new array reference without the deleted item
    } catch (err) {
      setError(`Failed to delete item '${itemName}'. Please try again.`);
      console.error("Delete item API failed:", err);
      // No UI rollback needed here because we update state *after* success
    } finally {
      // --- Clear Loading State ---
      setLoadingItemId(null);
    }
  };

  // --- Edit Handlers ---
  const startEdit = (item: Item) => {
    console.log("[ItemList] startEdit called", item.id);
    setEditingItemId(item.id);
    setEditName(item.name);
    setEditQuantity(item.quantity || "");
    setEditError(null);
    setSelectedCategory(
      item.categoryId
        ? { value: item.categoryId, label: item.categoryName }
        : null
    );
    setSelectedSubcategory(
      item.subCategoryId
        ? { value: item.subCategoryId, label: item.subCategoryName }
        : null
    );
  };

  const cancelEdit = () => {
    console.log("[ItemList] cancelEdit called");
    setEditingItemId(null);
    setEditName("");
    setEditQuantity("");
    setEditError(null);
    setSelectedCategory(null);
    setSelectedSubcategory(null);
  };

  const handleEditSubmit = async (e: React.FormEvent, item: Item) => {
    console.log("[ItemList] handleEditSubmit called", item.id);
    e.preventDefault();
    if (!editName.trim()) {
      setEditError("Item name is required.");
      return;
    }
    setLoadingItemId(item.id);
    setEditError(null);
    try {
      const apiUrl = `/api/items/${item.id}`;
      // Only send fields expected by backend DTO
      const payload = {
        name: editName.trim(),
        quantity: editQuantity.trim() || null,
        isChecked: item.isChecked,
        shoppingListId: item.shoppingListId,
        categoryId: selectedCategory?.value || null,
        subCategoryId: selectedSubcategory?.value || null,
      };
      await authenticatedFetch(apiUrl, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(payload),
      });
      // No local state update needed, parent/SignalR will handle it
      setEditingItemId(null);
    } catch (err) {
      setEditError("Failed to update item. Please try again.");
      console.error("Edit item API failed:", err);
    } finally {
      setLoadingItemId(null);
    }
  };

  // --- Bulk Delete Checked Items Handler ---
  const handleDeleteAllChecked = async () => {
    const checkedItems = initialItems.filter((item) => item.isChecked);
    if (checkedItems.length === 0) return;
    if (
      !window.confirm(
        `Are you sure you want to delete all ${checkedItems.length} checked item(s)? This cannot be undone.`
      )
    ) {
      return;
    }
    setLoadingItemId("__bulk__");
    setError(null);
    try {
      // Delete each checked item sequentially (could be parallelized if needed)
      for (const item of checkedItems) {
        await authenticatedFetch(`/api/items/${item.id}`, { method: "DELETE" });
      }
    } catch (err) {
      setError("Failed to delete one or more checked items. Please try again.");
      console.error("Bulk delete checked items failed:", err);
    } finally {
      setLoadingItemId(null);
    }
  };

  // --- Table Columns ---
  const columns = React.useMemo(() => {
    console.log(
      "ItemList: `columns` RE-CALCULATING. Categories loaded:",
      categories.length > 0,
      "Subcategories loaded:",
      subcategories.length > 0,
      "Editing Item:",
      editingItemId
    );
    return [
      {
        accessorKey: "isChecked",
        header: () => <span className="sr-only">Checked</span>,
        cell: (info: import("@tanstack/react-table").CellContext<Item, unknown>) => (
          <input
            type="checkbox"
            checked={info.row.original.isChecked}
            onChange={() => handleToggleCheck(info.row.original.id)}
            disabled={loadingItemId === info.row.original.id}
            className="h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
          />
        ),
        size: 32,
        enableSorting: false,
        meta: { filterVariant: "select" },
      },
      {
        accessorKey: "name",
        header: () => "Item Name",
        cell: (info: import("@tanstack/react-table").CellContext<Item, unknown>) => {
          if (editingItemId === info.row.original.id) {
            // *****************************************************************
            // **** CRITICAL TEST BLOCK ****
            // *****************************************************************
            if (subcategoryLoading) {
              // If subcategories are loading for the item being edited,
              // render a very simple placeholder INSTEAD of the full form.
              console.log(
                `[ITEMLIST CELL RENDER] Item: ${info.row.original.id} - EDITING & SUBCATEGORY LOADING. Rendering placeholder.`
              );
              return (
                <div style={{ padding: "10px", border: "2px solid red" }}>
                  Simplified: Subcategories Loading for {editName}...
                </div>
              );
            }
            // *****************************************************************
            // **** END OF CRITICAL TEST BLOCK ****
            // *****************************************************************

            // If not subcategoryLoading, render the full form
            console.log(
              `[ITEMLIST CELL RENDER] Item: ${info.row.original.id} - EDITING. Rendering full form. SubcategoryLoading: ${subcategoryLoading}`
            );
            return (
              <form
                onSubmit={(e) => handleEditSubmit(e, info.row.original)}
                className="flex items-center space-x-2"
              >
                <input
                  type="text"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  className="px-2 py-1 border rounded"
                  disabled={loadingItemId === info.row.original.id}
                  required
                />
                <input
                  type="text"
                  value={editQuantity}
                  onChange={(e) => setEditQuantity(e.target.value)}
                  className="px-2 py-1 border rounded"
                  disabled={loadingItemId === info.row.original.id}
                  placeholder="Quantity"
                />
                <Select
                  className="category-select"
                  isLoading={categoryLoading}
                  options={[{ value: "", label: "(None)" }, ...categories]}
                  value={selectedCategory || noneCategoryOption}
                  onChange={(option) => {
                    if (!option || !(option as CategoryOption).value) {
                      setSelectedCategory(null);
                      setSelectedSubcategory(null);
                    } else {
                      setSelectedCategory(option as CategoryOption);
                      setSelectedSubcategory(null);
                    }
                  }}
                  placeholder="Select category..."
                  isClearable
                  isDisabled={loadingItemId === info.row.original.id}
                />
                <Select
                  className="subcategory-select"
                  isLoading={subcategoryLoading}
                  options={[{ value: "", label: "(None)" }, ...subcategories]}
                  value={selectedSubcategory || noneSubcategoryOption}
                  onChange={(option) => {
                    setSelectedSubcategory(
                      option && (option as SubcategoryOption).value
                        ? (option as SubcategoryOption)
                        : null
                    );
                  }}
                  placeholder="Select subcategory..."
                  isClearable
                  isDisabled={
                    !selectedCategory || loadingItemId === info.row.original.id
                  }
                />
                <button
                  type="submit"
                  className="px-2 py-1 bg-blue-600 text-white rounded"
                  disabled={loadingItemId === info.row.original.id}
                >
                  Save
                </button>
                <button
                  type="button"
                  className="px-2 py-1 bg-gray-300 text-gray-800 rounded"
                  onClick={cancelEdit}
                  disabled={loadingItemId === info.row.original.id}
                >
                  Cancel
                </button>
                {editError && (
                  <span className="text-red-600 ml-2">{editError}</span>
                )}
              </form>
            );
          }
          return (
            <span
              className={clsx(
                "text-sm font-medium text-gray-900",
                info.row.original.isChecked && "line-through text-gray-500"
              )}
            >
              {info.row.original.name}
            </span>
          );
        },
        enableSorting: true,
      },
      {
        accessorKey: "quantity",
        header: () => "Quantity",
        cell: (info: import("@tanstack/react-table").CellContext<Item, unknown>) =>
          info.row.original.quantity ? (
            <span className="text-sm text-gray-500">
              {info.row.original.quantity}
            </span>
          ) : null,
        enableSorting: true,
      },
      {
        accessorKey: "categoryName",
        header: () => "Category",
        cell: (info: import("@tanstack/react-table").CellContext<Item, unknown>) => info.row.original.categoryName,
        enableSorting: true,
      },
      {
        accessorKey: "subCategoryName",
        header: () => "Subcategory",
        cell: (info: import("@tanstack/react-table").CellContext<Item, unknown>) => info.row.original.subCategoryName,
        enableSorting: true,
      },
      {
        id: "actions",
        header: () => "Actions",
        cell: (info: import("@tanstack/react-table").CellContext<Item, unknown>) =>
          editingItemId === info.row.original.id ? null : (
            <>
              <button
                onClick={() => startEdit(info.row.original)}
                disabled={loadingItemId === info.row.original.id}
                title={`Edit item "${info.row.original.name}"`}
                className={clsx(
                  "ml-2 p-1 text-sm font-medium text-blue-600 hover:text-blue-900 hover:bg-blue-100 rounded-md",
                  "focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-blue-500",
                  "disabled:opacity-50 disabled:cursor-not-allowed transition-all"
                )}
              >
                Edit
              </button>
              <button
                onClick={() =>
                  handleDeleteItem(info.row.original.id, info.row.original.name)
                }
                disabled={loadingItemId === info.row.original.id}
                title={`Delete item "${info.row.original.name}"`}
                className={clsx(
                  "ml-2 p-1 text-sm font-medium text-red-600 hover:text-red-900 hover:bg-red-100 rounded-md",
                  "focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-red-500",
                  "disabled:opacity-50 disabled:cursor-not-allowed transition-all"
                )}
              >
                <span className="sr-only">Delete {info.row.original.name}</span>
                Delete
              </button>
            </>
          ),
        enableSorting: false,
      },
    ];
  }, [
    editingItemId,
    editName,
    editQuantity,
    editError,
    loadingItemId,
    selectedCategory,
    selectedSubcategory,
    categories,
    subcategories,
    // checkedFilter removed from dependencies to keep columns stable
  ]);

  const table = useReactTable({
    data: filteredItems,
    columns,
    state: {
      sorting,
    },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    //getFilteredRowModel: getFilteredRowModel(),
    getRowId: (row) => row.id,
    getSubRows: undefined,
  });

  // --- Render Logic with Tailwind ---
  return (
    <div className="bg-white shadow overflow-hidden sm:rounded-md">
      <h3 className="text-lg font-medium leading-6 text-gray-900 px-4 py-3 sm:px-6 border-b border-gray-200">
        Shopping Items
        <span className="ml-2 text-sm font-normal text-gray-500">
          ({!initialItems ? 0 :initialItems.length})
        </span>
      </h3>
      {/* Checked/Unchecked Filter Dropdown moved above the table */}
      <div className="px-4 py-2 flex items-center gap-2">
        <label
          htmlFor="checkedFilter"
          className="mr-2 text-xs font-medium text-gray-700"
        >
          Show:
        </label>
        <select
          id="checkedFilter"
          className="text-xs border rounded px-1 py-0.5"
          value={checkedFilter}
          onChange={(e) => setCheckedFilter(e.target.value)}
          style={{ minWidth: 70 }}
        >
          <option value="all">All</option>
          <option value="checked">Checked</option>
          <option value="unchecked">Unchecked</option>
        </select>
        <button
          type="button"
          className="ml-2 px-2 py-1 text-xs bg-red-600 text-white rounded disabled:opacity-50 disabled:cursor-not-allowed"
          onClick={handleDeleteAllChecked}
          disabled={
            !initialItems || initialItems.filter((item) => item.isChecked).length === 0 ||
            loadingItemId === "__bulk__"
          }
          title="Delete all checked items"
        >
          Delete All Checked
        </button>
      </div>
      {error && (
        <div className="p-4 border-b border-gray-200">
          <p className="text-sm text-red-700 bg-red-100 p-3 rounded border border-red-300">
            {error}
          </p>
        </div>
      )}
      {(!initialItems || initialItems.length === 0) && !error && (
        <p className="px-4 py-4 text-sm text-gray-500 sm:px-6 italic">
          This list is empty.
        </p>
      )}
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <th
                    key={header.id}
                    className={clsx(
                      "px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider",
                      header.column.getCanSort() && "cursor-pointer select-none"
                    )}
                    style={header.getSize ? { width: header.getSize() } : {}}
                    onClick={header.column.getToggleSortingHandler?.()}
                  >
                    {header.isPlaceholder ? null : (
                      <>
                        {flexRender(
                          header.column.columnDef.header,
                          header.getContext()
                        )}
                        {header.column.getIsSorted() === "asc" && (
                          <span> ▲</span>
                        )}
                        {header.column.getIsSorted() === "desc" && (
                          <span> ▼</span>
                        )}
                      </>
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {table.getRowModel().rows.map((row: Row<Item>) => {
              // console.log('[ItemList] rendering row', row.id); // Removed noisy log
              return (
                <tr
                  key={row.id}
                  className={
                    loadingItemId === row.original.id
                      ? "opacity-50"
                      : "opacity-100"
                  }
                >
                  {row.getVisibleCells().map((cell: Cell<Item, unknown>) => {
                    // console.log('[ItemList] rendering cell', cell.id); // Removed noisy log
                    return (
                      <td key={cell.id} className="px-4 py-2 align-middle">
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext()
                        )}
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ItemList;
