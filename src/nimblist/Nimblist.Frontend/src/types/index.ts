export interface Item {
    id: string;
    name: string;
    quantity?: string | null;
    isChecked: boolean;
    addedAt: string; // Or Date
    shoppingListId: string;
  }
  
  export interface ShoppingList {
    id: string;
    name: string;
    createdAt: string; // Or Date
    userId: string;
    items: Item[]; // Ensure this is defined
  }