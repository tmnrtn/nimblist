import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { authenticatedFetch } from "../components/HttpHelper";
import RecipesPage from "./RecipesPage";
import type { RecipeSummary } from "../types";

vi.mock("../components/HttpHelper");

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

const recipe1: RecipeSummary = {
  id: "r1", title: "Pasta Bolognese", imageUrl: null,
  yields: "4 servings", totalTimeMinutes: 30,
  ingredientCount: 5, createdAt: "2026-01-01T00:00:00Z", isOwned: true,
};
const recipe2: RecipeSummary = {
  id: "r2", title: "Shared Recipe", imageUrl: null,
  yields: null, totalTimeMinutes: null,
  ingredientCount: 2, createdAt: "2026-01-02T00:00:00Z", isOwned: false,
};

function renderPage() {
  return render(<MemoryRouter><RecipesPage /></MemoryRouter>);
}

describe("RecipesPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    vi.spyOn(window, "confirm").mockReturnValue(true);
  });

  it("shows loading state then renders recipes", async () => {
    mockFetch.mockReturnValue(jsonResponse([recipe1, recipe2]));
    renderPage();
    await waitFor(() => expect(screen.getByText("Pasta Bolognese")).toBeInTheDocument());
    expect(screen.getByText("Shared Recipe")).toBeInTheDocument();
  });

  it("shows 'no recipes' message when list is empty", async () => {
    mockFetch.mockReturnValue(jsonResponse([]));
    renderPage();
    await waitFor(() => expect(screen.getByText(/no recipes yet/i)).toBeInTheDocument());
  });

  it("shows error when fetch fails", async () => {
    mockFetch.mockRejectedValue(new Error("Network error"));
    renderPage();
    await waitFor(() => expect(screen.getByText(/failed to load recipes/i)).toBeInTheDocument());
  });

  it("renders View link for each recipe", async () => {
    mockFetch.mockReturnValue(jsonResponse([recipe1]));
    renderPage();
    await waitFor(() => expect(screen.getByRole("link", { name: /view/i })).toBeInTheDocument());
  });

  it("only shows Delete button for owned recipes", async () => {
    mockFetch.mockReturnValue(jsonResponse([recipe1, recipe2]));
    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    expect(deleteButtons).toHaveLength(1);
  });

  it("switches to manual create tab", async () => {
    mockFetch.mockReturnValue(jsonResponse([]));
    renderPage();
    await waitFor(() => screen.getByText(/import from url/i));
    fireEvent.click(screen.getByRole("button", { name: /create manually/i }));
    expect(screen.getByRole("button", { name: /save recipe/i })).toBeInTheDocument();
  });

  it("import form submits and prepends recipe to list", async () => {
    const newRecipe = { id: "r3", title: "Imported", imageUrl: null, yields: null,
      totalTimeMinutes: null, ingredients: [], createdAt: "2026-01-03T00:00:00Z" };
    mockFetch
      .mockReturnValueOnce(jsonResponse([recipe1]))
      .mockReturnValueOnce(jsonResponse(newRecipe, true, 201));

    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));

    fireEvent.change(screen.getByPlaceholderText(/https:\/\/www.example.com/i), {
      target: { value: "https://example.com/recipe" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^import$/i }));

    await waitFor(() => expect(screen.getByText("Imported")).toBeInTheDocument());
  });

  it("shows import error when import fails", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse({ error: "Scraper failed" }, false, 422));

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
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse(newRecipe, true, 201));

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
    mockFetch
      .mockReturnValueOnce(jsonResponse([recipe1]))
      .mockReturnValueOnce(Promise.resolve({ ok: true, status: 204 } as Response));

    renderPage();
    await waitFor(() => screen.getByText("Pasta Bolognese"));
    fireEvent.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => expect(screen.queryByText("Pasta Bolognese")).not.toBeInTheDocument());
  });

  it("ingredient rows can be added and removed in manual form", async () => {
    mockFetch.mockReturnValue(jsonResponse([]));
    renderPage();
    await waitFor(() => screen.getByText(/no recipes yet/i));

    fireEvent.click(screen.getByRole("button", { name: /create manually/i }));
    expect(screen.getAllByPlaceholderText(/e.g. 2 cups flour/i)).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /\+ add ingredient/i }));
    expect(screen.getAllByPlaceholderText(/e.g. 2 cups flour/i)).toHaveLength(2);

    fireEvent.click(screen.getAllByRole("button", { name: /remove ingredient/i })[0]);
    expect(screen.getAllByPlaceholderText(/e.g. 2 cups flour/i)).toHaveLength(1);
  });
});
