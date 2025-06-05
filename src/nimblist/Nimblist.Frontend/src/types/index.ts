export interface Item {
    id: string;
    name: string;
    quantity?: string | null;
    isChecked: boolean;
    addedAt: string; // Or Date
    shoppingListId: string;
    categoryName: string; // Ensure this is defined
    subCategoryName: string; // Ensure this is defined
    categoryId?: string | null; // <-- PATCHED: for edit support
    subCategoryId?: string | null; // <-- PATCHED: for edit support
  }
  
  export interface ShoppingList {
    id: string;
    name: string;
    createdAt: string; // Or Date
    userId: string;
    items: Item[]; // Ensure this is defined
  }