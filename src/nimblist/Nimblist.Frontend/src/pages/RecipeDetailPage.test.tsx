import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { authenticatedFetch } from "../components/HttpHelper";
import RecipeDetailPage from "./RecipeDetailPage";
import type { RecipeDetail, ShoppingList } from "../types";

vi.mock("../components/HttpHelper");
vi.mock("../components/SharePanel", () => ({
  default: ({ isOwner }: { isOwner: boolean }) => (
    <div data-testid="share-panel">{isOwner ? "owner-share" : "non-owner-share"}</div>
  ),
}));
vi.mock("../components/ImageSearchModal", () => ({
  default: () => null,
}));

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

const recipe: RecipeDetail = {
  id: "r1", title: "Pasta", description: "A classic.",
  sourceUrl: "https://example.com", imageUrl: null,
  yields: "4 servings", totalTimeMinutes: 30,
  instructions: "Boil water.\nCook pasta.",
  createdAt: "2026-01-01T00:00:00Z",
  ingredients: [
    { id: "i1", text: "200g pasta", parsedName: "pasta", parsedQuantity: "200g", sortOrder: 0 },
    { id: "i2", text: "Tomato sauce", parsedName: null, parsedQuantity: null, sortOrder: 1 },
  ],
  isOwned: true,
  tags: [],
};

const list1: ShoppingList = {
  id: "sl1", name: "Groceries", createdAt: "2026-01-01T00:00:00Z", userId: "u1", items: [],
};

/** Mock the 3 initial-load calls: recipe, shoppinglists, tags */
function mockLoad(r = recipe, lists = [list1]) {
  mockFetch
    .mockReturnValueOnce(jsonResponse(r))
    .mockReturnValueOnce(jsonResponse(lists))
    .mockReturnValueOnce(jsonResponse([])); // /api/tags
}

function renderPage(recipeId = "r1") {
  return render(
    <MemoryRouter initialEntries={[`/recipes/${recipeId}`]}>
      <Routes>
        <Route path="/recipes/:recipeId" element={<RecipeDetailPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("RecipeDetailPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows loading state initially", () => {
    mockFetch.mockReturnValue(new Promise(() => {}));
    renderPage();
    expect(screen.getByText(/loading recipe/i)).toBeInTheDocument();
  });

  it("renders recipe title and description", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => expect(screen.getByText("Pasta")).toBeInTheDocument());
    expect(screen.getByText("A classic.")).toBeInTheDocument();
  });

  it("renders ingredients list", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => screen.getByText("Pasta"));
    expect(screen.getByText("pasta")).toBeInTheDocument();
    expect(screen.getByText("Tomato sauce")).toBeInTheDocument();
  });

  it("renders instruction steps", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => screen.getByText("Boil water."));
    expect(screen.getByText("Cook pasta.")).toBeInTheDocument();
  });

  it("shows Edit button for owned recipes", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument());
  });

  it("hides Edit button for non-owned recipes", async () => {
    mockLoad({ ...recipe, isOwned: false });
    renderPage();
    await waitFor(() => screen.getByText("Pasta"));
    expect(screen.queryByRole("button", { name: /edit/i })).not.toBeInTheDocument();
  });

  it("shows error message when load fails", async () => {
    mockFetch.mockRejectedValue(new Error("Network error"));
    renderPage();
    await waitFor(() => expect(screen.getByText(/failed to load recipe/i)).toBeInTheDocument());
  });

  it("entering edit mode pre-fills title", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => screen.getByRole("button", { name: /edit/i }));
    fireEvent.click(screen.getByRole("button", { name: /edit/i }));

    expect(screen.getByDisplayValue("Pasta")).toBeInTheDocument();
    expect(screen.getByDisplayValue("A classic.")).toBeInTheDocument();
  });

  it("cancel edit returns to view mode", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => screen.getByRole("button", { name: /edit/i }));
    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    expect(screen.getByText("Edit Recipe")).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: /^cancel$/i })[0]);
    await waitFor(() => expect(screen.queryByText("Edit Recipe")).not.toBeInTheDocument());
  });

  it("save PUT request updates recipe and exits edit mode", async () => {
    const updated = { ...recipe, title: "Updated Pasta" };
    mockLoad();
    // PUT response (save changes)
    mockFetch.mockReturnValueOnce(jsonResponse(updated));

    renderPage();
    await waitFor(() => screen.getByRole("button", { name: /edit/i }));
    fireEvent.click(screen.getByRole("button", { name: /edit/i }));

    fireEvent.change(screen.getByDisplayValue("Pasta"), { target: { value: "Updated Pasta" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => expect(screen.getByText("Updated Pasta")).toBeInTheDocument());
    expect(screen.queryByText("Edit Recipe")).not.toBeInTheDocument();
  });

  it("shows save error on failed PUT", async () => {
    mockLoad();
    // PUT response (fails)
    mockFetch.mockReturnValueOnce(jsonResponse({ title: "Validation failed" }, false, 400));

    renderPage();
    await waitFor(() => screen.getByRole("button", { name: /edit/i }));
    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => expect(screen.getByText(/validation failed/i)).toBeInTheDocument());
  });

  it("shows list dropdown and Add to list button when lists exist", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => expect(screen.getByRole("button", { name: /add to list/i })).toBeInTheDocument());
    expect(screen.getByRole("option", { name: "Groceries" })).toBeInTheDocument();
  });

  it("shows 'no lists yet' when lists are empty", async () => {
    mockLoad(recipe, []);
    renderPage();
    await waitFor(() => expect(screen.getByText(/no lists yet/i)).toBeInTheDocument());
  });

  it("Add to list shows ingredient count on success", async () => {
    mockLoad();
    // addtolist POST response
    mockFetch.mockReturnValueOnce(jsonResponse({ addedCount: 2 }));

    renderPage();
    await waitFor(() => screen.getByRole("button", { name: /add to list/i }));
    fireEvent.click(screen.getByRole("button", { name: /add to list/i }));

    await waitFor(() => expect(screen.getByText(/added 2 ingredients to list/i)).toBeInTheDocument());
  });

  it("shows SharePanel for recipes", async () => {
    mockLoad();
    renderPage();
    await waitFor(() => expect(screen.getByTestId("share-panel")).toBeInTheDocument());
  });
});
