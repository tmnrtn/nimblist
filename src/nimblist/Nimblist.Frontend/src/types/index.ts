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
    recipeId?: string | null;
    recipeTitle?: string | null;
  }
  
  export interface ShoppingList {
    id: string;
    name: string;
    createdAt: string;
    userId: string;
    items: Item[];
  }

  export interface RecipeIngredient {
    id: string;
    text: string;
    parsedName: string | null;
    parsedQuantity: string | null;
    sortOrder: number;
  }

  export interface RecipeSummary {
    id: string;
    title: string;
    imageUrl: string | null;
    yields: string | null;
    totalTimeMinutes: number | null;
    ingredientCount: number;
    createdAt: string;
    isOwned: boolean;
  }

  export interface FamilyMemberDetail {
    id: string;
    userId: string;
    email: string | null;
    role: string;
    joinedAt: string;
    isOwner: boolean;
  }

  export interface Family {
    id: string;
    name: string;
    ownerId: string;
    members: FamilyMemberDetail[];
  }

  export interface ListShareDetail {
    id: string;
    listId: string;
    sharedWithUserId: string | null;
    sharedWithEmail: string | null;
    sharedWithFamilyId: string | null;
    sharedWithFamilyName: string | null;
    sharedAt: string;
  }

  export interface MealPlanSummary {
    id: string;
    name: string;
    ownerId: string;
    isOwned: boolean;
    createdAt: string;
  }

  export interface MealPlanEntry {
    id: string;
    mealPlanId: string;
    recipeId: string;
    recipeTitle: string;
    recipeImageUrl: string | null;
    plannedDate: string; // "YYYY-MM-DD"
    mealType: string | null;
    notes: string | null;
  }

  export interface MealPlanShareDetail {
    id: string;
    mealPlanId: string;
    sharedWithUserId: string | null;
    sharedWithEmail: string | null;
    sharedWithFamilyId: string | null;
    sharedWithFamilyName: string | null;
    sharedAt: string;
  }

  export interface RecipeShareDetail {
    id: string;
    recipeId: string;
    sharedWithUserId: string | null;
    sharedWithEmail: string | null;
    sharedWithFamilyId: string | null;
    sharedWithFamilyName: string | null;
    sharedAt: string;
  }

  export interface RecipeDetail {
    id: string;
    title: string;
    description: string | null;
    sourceUrl: string | null;
    imageUrl: string | null;
    yields: string | null;
    totalTimeMinutes: number | null;
    instructions: string | null;
    createdAt: string;
    ingredients: RecipeIngredient[];
    isOwned: boolean;
  }