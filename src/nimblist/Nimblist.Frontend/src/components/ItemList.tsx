// src/components/ItemList.tsx
import React, { useState, useEffect } from "react";
import { Item } from "../types/index";
import { authenticatedFetch } from "./HttpHelper";
import clsx from "clsx";
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
  listId: string;
  // Parent-provided mutation handlers (for optimistic updates)
  onDeleteItem: (itemId: string, itemName: string) => void;
  onEditItem: (item: Item, update: { name: string; quantity: string | null; categoryId: string | null; subCategoryId: string | null; isChecked?: boolean }) => void;
  onDeleteAllChecked: () => void;
  error?: string | null;
  bulkDeleteLoading?: boolean;
}

// --- Category/Subcategory fetch types ---
interface CategoryApi {
  id: string;
  name: string;
}
interface SubCategoryApi {
  id: string;
  name: string;
  parentCategoryId: string;
}
// --- End Helper ---

const ItemList: React.FC<ItemListProps> = ({
  initialItems,
  onDeleteItem,
  onEditItem,
  onDeleteAllChecked,
  error,
  bulkDeleteLoading = false,
}) => {
  // --- State ---
  // Remove error state, keep only UI state
  // const [error, setError] = useState<string | null>(null);
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
    setCategoryLoading(true);
    authenticatedFetch("/api/categories", {
      method: "GET",
    })
      .then((res: Response) => res.json())
      .then((data: CategoryApi[]) => {
        setCategories(data.map((cat) => ({ value: cat.id, label: cat.name })));
      })
      .catch(() => {
        setEditError("Failed to load categories");
      })
      .finally(() => {
        setCategoryLoading(false);
      });
  }, []);

  // Fetch subcategories when a category is selected
  useEffect(() => {
    if (selectedCategory) {
      setSubcategoryLoading(true);
      authenticatedFetch(
        `/api/subcategories?parentCategoryId=${selectedCategory.value}`,
        {
          method: "GET",
        }
      )
        .then((res: Response) => res.json())
        .then((data: SubCategoryApi[]) => {
          setSubcategories(data.map((sub) => ({ value: sub.id, label: sub.name })));
        })
        .catch(() => {
          setEditError("Failed to load subcategories");
        })
        .finally(() => {
          setSubcategoryLoading(false);
        });
    } else {
      setSubcategories([]);
    }
  }, [selectedCategory]);

  // --- Toggle Handler ---
  const handleToggleCheck = (itemId: string): void => {
    setLoadingItemId(itemId);
    // Find the item to get its current state
    const item = initialItems.find((i) => i.id === itemId);
    if (!item) return;
    // Call onEditItem with isChecked toggled
    onEditItem(item, {
      name: item.name,
      quantity: item.quantity || null,
      categoryId: item.categoryId || null,
      subCategoryId: item.subCategoryId || null,
      isChecked: !item.isChecked,
    });
    // Parent will clear loading state via props update
  };

  // --- Delete Handler ---
  const handleDeleteItem = (
    itemIdToDelete: string,
    itemName: string
  ): void => {
    if (!window.confirm(`Are you sure you want to delete "${itemName}"?`)) {
      return;
    }
    setLoadingItemId(itemIdToDelete);
    onDeleteItem(itemIdToDelete, itemName);
    // Parent will clear loading state via props update
  };

  // --- Edit Handlers ---
  // Track the last saved values when entering edit mode
  const [lastSaved, setLastSaved] = useState<{
    id: string;
    name: string;
    quantity: string | null;
    categoryId: string | null | undefined;
    subCategoryId: string | null | undefined;
  } | null>(null);

  const startEdit = (item: Item) => {
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
    setLastSaved({
      id: item.id,
      name: item.name,
      quantity: item.quantity || "",
      categoryId: item.categoryId,
      subCategoryId: item.subCategoryId,
    });
  };

  const cancelEdit = () => {
    console.log("[ItemList] cancelEdit called");
    setEditingItemId(null);
    setEditName("");
    setEditQuantity("");
    setEditError(null);
    setSelectedCategory(null);
    setSelectedSubcategory(null);
    setLastSaved(null);
  };

  const handleEditSubmit = (
    e: React.FormEvent,
    item: Item
  ) => {
    console.log("[ItemList] handleEditSubmit called", item.id);
    e.preventDefault();
    if (!editName.trim()) {
      setEditError("Item name is required.");
      return;
    }
    setLoadingItemId(item.id);
    onEditItem(item, {
      name: editName.trim(),
      quantity: editQuantity.trim() || null,
      categoryId: selectedCategory?.value || null,
      subCategoryId: selectedSubcategory?.value || null,
    });
    // Parent will clear loading state via props update
  };

  // --- Bulk Delete Checked Items Handler ---
  const handleDeleteAllChecked = () => {
    if (
      !window.confirm(
        `Are you sure you want to delete all checked item(s)? This cannot be undone.`
      )
    ) {
      return;
    }
    setLoadingItemId("__bulk__");
    onDeleteAllChecked();
    // Parent will clear loading state via props update
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
            // Instead of rendering the form in just this cell, render the form spanning the whole row
            // We'll handle this in the row rendering below
            return null;
          }
          // --- Normal cell: show name, and quantity in small text if present ---
          return (
            <span className={clsx(
              "text-sm font-medium text-gray-900",
              info.row.original.isChecked && "line-through text-gray-500"
            )}>
              {info.row.original.name}
              {info.row.original.quantity && (
                <span className="ml-1 text-xs text-gray-500">({info.row.original.quantity})</span>
              )}
            </span>
          );
        },
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

  // --- Auto-close edit form when the item is updated in initialItems
  useEffect(() => {
    if (!editingItemId || !lastSaved) return;
    const edited = initialItems.find((i) => i.id === editingItemId);
    if (!edited) {
      setEditingItemId(null);
      setEditName("");
      setEditQuantity("");
      setEditError(null);
      setSelectedCategory(null);
      setSelectedSubcategory(null);
      setLoadingItemId(null);
      setLastSaved(null);
      return;
    }
    // Only close if the item in the list is different from the last saved values
    if (
      edited.name !== lastSaved.name ||
      (edited.quantity || "") !== (lastSaved.quantity || "") ||
      (edited.categoryId || "") !== (lastSaved.categoryId || "") ||
      (edited.subCategoryId || "") !== (lastSaved.subCategoryId || "")
    ) {
      setEditingItemId(null);
      setEditName("");
      setEditQuantity("");
      setEditError(null);
      setSelectedCategory(null);
      setSelectedSubcategory(null);
      setLoadingItemId(null);
      setLastSaved(null);
    }
  }, [initialItems, editingItemId, lastSaved]);

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
            loadingItemId === "__bulk__" ||
            bulkDeleteLoading
          }
          title="Delete all checked items"
        >
          {bulkDeleteLoading ? "Deleting..." : "Delete All Checked"}
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
              if (editingItemId === row.original.id) {
                // Render a single cell spanning all columns with the edit form
                return (
                  <tr key={row.id}>
                    <td colSpan={table.getAllColumns().length} className="px-4 py-2 align-middle bg-blue-50">
                      {/* 4-row stacked edit form as before */}
                      <form
                        onSubmit={(e) => handleEditSubmit(e, row.original)}
                        className="flex flex-col gap-2 w-full"
                      >
                        {/* Row 1: Item name and quantity */}
                        <div className="flex flex-col sm:flex-row gap-2">
                          <input
                            type="text"
                            value={editName}
                            onChange={(e) => setEditName(e.target.value)}
                            className="px-2 py-1 border rounded flex-1"
                            disabled={loadingItemId === row.original.id}
                            required
                            placeholder="Item name"
                          />
                          <input
                            type="text"
                            value={editQuantity}
                            onChange={(e) => setEditQuantity(e.target.value)}
                            className="px-2 py-1 border rounded flex-1"
                            disabled={loadingItemId === row.original.id}
                            placeholder="Quantity"
                          />
                        </div>
                        {/* Row 2: Category */}
                        <div>
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
                            isDisabled={loadingItemId === row.original.id}
                          />
                        </div>
                        {/* Row 3: Subcategory */}
                        <div>
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
                              !selectedCategory || loadingItemId === row.original.id
                            }
                          />
                        </div>
                        {/* Row 4: Save/Cancel buttons */}
                        <div className="flex gap-2">
                          <button
                            type="submit"
                            className="px-2 py-1 bg-blue-600 text-white rounded flex-1"
                            disabled={loadingItemId === row.original.id}
                          >
                            Save
                          </button>
                          <button
                            type="button"
                            className="px-2 py-1 bg-gray-300 text-gray-800 rounded flex-1"
                            onClick={cancelEdit}
                            disabled={loadingItemId === row.original.id}
                          >
                            Cancel
                          </button>
                          {editError && (
                            <span className="text-red-600 ml-2">{editError}</span>
                          )}
                        </div>
                      </form>
                    </td>
                  </tr>
                );
              }
              // ...existing code for normal row rendering...
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
