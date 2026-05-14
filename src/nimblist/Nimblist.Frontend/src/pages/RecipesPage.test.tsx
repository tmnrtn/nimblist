import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { authenticatedFetch } from "../components/HttpHelper";
import RecipesPage from "./RecipesPage";
import type { RecipeSummary, ShoppingList } from "../types";

vi.mock("../components/HttpHelper");

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

const recipe1: RecipeSummary = {
  id: "r1", title: "Pasta Bolognese", imageUrl: null,
  yields: "4 servings", totalTimeMinutes: 30,
  ingredientCount: 5, createdAt: "2026-01-01T00:00:00Z", isOwned: true, tags: [],
};
const recipe2: RecipeSummary = {
  id: "r2", title: "Shared Recipe", imageUrl: null,
  yields: null, totalTimeMinutes: null,
  ingredientCount: 2, createdAt: "2026-01-02T00:00:00Z", isOwned: false, tags: [],
};
const list1: ShoppingList = {
  id: "l1", name: "My List", createdAt: "2026-01-01T00:00:00Z", userId: "u1", items: []
};

function renderPage() {
  return render(<MemoryRouter><RecipesPage /></MemoryRouter>);
}

describe("RecipesPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    // Default mock implementation
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      if (url === '/api/tags') return jsonResponse([]);
      return jsonResponse(null);
    });
  });

  it("shows loading state then renders recipes", async () => {
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([recipe1, recipe2]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      return jsonResponse(null);
    });
    renderPage();
    await waitFor(() => expect(screen.getByText("Pasta Bolognese")).toBeInTheDocument());
    expect(screen.getByText("Shared Recipe")).toBeInTheDocument();
  });

  it("shows 'no recipes' message when list is empty", async () => {
    // Default implementation handles empty recipes
    renderPage();
    await waitFor(() => expect(screen.getByText(/no recipes yet/i)).toBeInTheDocument());
  });

  it("shows error when fetch fails", async () => {
    mockFetch.mockRejectedValue(new Error("Network error"));
    renderPage();
    await waitFor(() => expect(screen.getByText(/failed to load recipes/i)).toBeInTheDocument());
  });

  it("renders View link for each recipe", async () => {
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([recipe1]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      return jsonResponse(null);
    });
    renderPage();
    await waitFor(() => expect(screen.getByRole("link", { name: /view/i })).toBeInTheDocument());
  });

  it("only shows Delete button for owned recipes", async () => {
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([recipe1, recipe2]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      return jsonResponse(null);
    });
    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    expect(deleteButtons).toHaveLength(1);
  });

  it("switches to manual create tab", async () => {
    renderPage();
    await waitFor(() => screen.getByText(/import from url/i));
    fireEvent.click(screen.getByRole("button", { name: /create manually/i }));
    expect(screen.getByRole("button", { name: /save recipe/i })).toBeInTheDocument();
  });

  it("import form submits and prepends recipe to list", async () => {
    const newRecipe = { id: "r3", title: "Imported", imageUrl: null, yields: null,
      totalTimeMinutes: null, ingredients: [], createdAt: "2026-01-03T00:00:00Z" };
    
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([recipe1]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      if (url === '/api/recipes/import') return jsonResponse(newRecipe, true, 201);
      return jsonResponse(null);
    });

    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));

    fireEvent.change(screen.getByPlaceholderText(/https:\/\/www.example.com/i), {
      target: { value: "https://example.com/recipe" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^import$/i }));

    await waitFor(() => expect(screen.getByText("Imported")).toBeInTheDocument());
  });

  it("shows import error when import fails", async () => {
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      if (url === '/api/recipes/import') return jsonResponse({ error: "Scraper failed" }, false, 422);
      return jsonResponse(null);
    });

    renderPage();
    await waitFor(() => screen.getByText(/no recipes yet/i));

    fireEvent.change(screen.getByPlaceholderText(/https:\/\/www.example.com/i), {
      target: { value: "https://example.com/recipe" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^import$/i }));

    await waitFor(() => expect(screen.getByText(/scraper failed/i)).toBeInTheDocument());
  });

  it("manual create form submits and prepends recipe", async () => {
    const newRecipe = { id: "r4", title: "My Recipe", imageUrl: null, yields: null,
      totalTimeMinutes: null, ingredients: [{}], createdAt: "2026-01-04T00:00:00Z" };
    
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      if (url === '/api/recipes' && !url.includes('api/recipes/')) return jsonResponse(newRecipe, true, 201); // Simplified check for POST
      return jsonResponse(null);
    });
    
    // Better implementation for dynamic tests
    mockFetch.mockImplementation((url, init) => {
      if (url === '/api/recipes') {
        if (init?.method === 'POST') return jsonResponse(newRecipe, true, 201);
        return jsonResponse([]);
      }
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      return jsonResponse(null);
    });

    renderPage();
    await waitFor(() => screen.getByText(/no recipes yet/i));

    fireEvent.click(screen.getByRole("button", { name: /create manually/i }));
    // Title is the first unlabeled text input in the manual form
    fireEvent.change(screen.getAllByRole("textbox")[0], {
      target: { value: "My Recipe" },
    });
    fireEvent.click(screen.getByRole("button", { name: /save recipe/i }));

    await waitFor(() => expect(screen.getByText("My Recipe")).toBeInTheDocument());
  });

  it("delete removes recipe from list after confirmation", async () => {
    mockFetch.mockImplementation((url, init) => {
      if (url === '/api/recipes') return jsonResponse([recipe1]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      if (url === `/api/recipes/${recipe1.id}` && init?.method === 'DELETE') {
        return Promise.resolve({ ok: true, status: 204 } as Response);
      }
      return jsonResponse(null);
    });

    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));
    fireEvent.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => expect(screen.queryByText("Pasta Bolognese")).not.toBeInTheDocument());
  });

  it("ingredient rows can be added and removed in manual form", async () => {
    renderPage();
    await waitFor(() => screen.getByText(/no recipes yet/i));

    fireEvent.click(screen.getByRole("button", { name: /create manually/i }));
    expect(screen.getAllByPlaceholderText(/e.g. 2 cups flour/i)).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /\+ add ingredient/i }));
    expect(screen.getAllByPlaceholderText(/e.g. 2 cups flour/i)).toHaveLength(2);

    fireEvent.click(screen.getAllByRole("button", { name: /remove ingredient/i })[0]);
    expect(screen.getAllByPlaceholderText(/e.g. 2 cups flour/i)).toHaveLength(1);
  });

  it("shows list dropdown and Add to list button when lists exist", async () => {
    mockFetch.mockImplementation((url) => {
      if (url === '/api/recipes') return jsonResponse([recipe1]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      return jsonResponse(null);
    });

    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));

    // Click "Add ingredients to list" button
    fireEvent.click(screen.getByText(/add ingredients to list/i));

    // Should show dropdown and Add button
    expect(screen.getByRole("combobox")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^add$/i })).toBeInTheDocument();
  });

  it("Add to list shows success message on ok response", async () => {
    mockFetch.mockImplementation((url, init) => {
      if (url === '/api/recipes') return jsonResponse([recipe1]);
      if (url === '/api/shoppinglists') return jsonResponse([list1]);
      if (url === `/api/recipes/${recipe1.id}/addtolist/${list1.id}` && init?.method === 'POST') {
        return jsonResponse({ addedCount: 5 });
      }
      return jsonResponse(null);
    });

    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));

    fireEvent.click(screen.getByText(/add ingredients to list/i));
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    await waitFor(() => expect(screen.getByText(/added 5 items/i)).toBeInTheDocument());
  });
});

