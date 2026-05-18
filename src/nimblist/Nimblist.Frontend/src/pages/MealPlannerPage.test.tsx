import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { MockedFunction } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { authenticatedFetch } from "../components/HttpHelper";
import MealPlannerPage from "./MealPlannerPage";
import type { MealPlanSummary, MealPlanEntry, RecipeSummary, ShoppingList } from "../types";

vi.mock("../components/HttpHelper");
vi.mock("../components/SharePanel", () => ({
  default: () => <div data-testid="share-panel" />,
}));
vi.mock("../store/authStore", () => ({
  default: () => ({ isPaid: true }),
}));

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true) {
  return Promise.resolve({ ok, json: () => Promise.resolve(data) } as Response);
}

const plan1: MealPlanSummary = { id: "p1", name: "Week 1", ownerId: "u1", isOwned: true, createdAt: "" };
const recipe1: RecipeSummary = { id: "r1", title: "Pasta", imageUrl: null, yields: null, totalTimeMinutes: null, ingredientCount: 2, createdAt: "", isOwned: true, tags: [] };
const list1: ShoppingList = { id: "sl1", name: "Groceries", createdAt: "", userId: "u1", items: [] };

// Monday of the week containing 2026-05-04
const entry1: MealPlanEntry = {
  id: "e1", mealPlanId: "p1", recipeId: "r1", recipeTitle: "Pasta",
  recipeImageUrl: null, plannedDate: "2026-05-04", mealType: "Dinner", notes: null,
};

function mockInitialLoad(plans = [plan1], recipes = [recipe1], lists = [list1]) {
  mockFetch
    .mockReturnValueOnce(jsonResponse(plans))
    .mockReturnValueOnce(jsonResponse(recipes))
    .mockReturnValueOnce(jsonResponse(lists));
}

function renderPage() {
  return render(<MemoryRouter><MealPlannerPage /></MemoryRouter>);
}

describe("MealPlannerPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    // Fake only Date so waitFor's internal timers still work
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-05-06T12:00:00Z"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows loading then renders plan selector", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([])); // entries
    renderPage();
    await waitFor(() => expect(screen.getByText("Week 1")).toBeInTheDocument());
  });

  it("shows 'no meal plans' message when plans list is empty", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([recipe1]))
      .mockReturnValueOnce(jsonResponse([list1]));
    // No entries call when no plan selected
    renderPage();
    await waitFor(() => expect(screen.getByText(/no meal plans yet/i)).toBeInTheDocument());
  });

  it("renders calendar with 7 day columns", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([]));
    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    // Days Mon-Sun rendered
    expect(screen.getByText(/Mon/)).toBeInTheDocument();
    expect(screen.getByText(/Sun/)).toBeInTheDocument();
  });

  it("renders entries in the calendar", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([entry1]));
    renderPage();
    await waitFor(() => expect(screen.getByText("Pasta")).toBeInTheDocument());
  });

  it("shows + New Plan button and toggles form", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([]));
    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    fireEvent.click(screen.getByRole("button", { name: /\+ new plan/i }));
    expect(screen.getByPlaceholderText(/plan name/i)).toBeInTheDocument();
  });

  it("creates a new plan and selects it", async () => {
    const newPlan: MealPlanSummary = { id: "p2", name: "Week 2", ownerId: "u1", isOwned: true, createdAt: "" };
    mockInitialLoad();
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))   // initial entries
      .mockReturnValueOnce(jsonResponse(newPlan)) // create plan
      .mockReturnValueOnce(jsonResponse([]));  // entries for new plan

    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    fireEvent.click(screen.getByRole("button", { name: /\+ new plan/i }));
    fireEvent.change(screen.getByPlaceholderText(/plan name/i), { target: { value: "Week 2" } });
    fireEvent.click(screen.getByRole("button", { name: /^create$/i }));

    await waitFor(() => expect(screen.getByText("Week 2")).toBeInTheDocument());
  });

  it("clicking + Add opens modal entry form for that day", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([]));
    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    const addButtons = screen.getAllByRole("button", { name: /\+ add/i });
    fireEvent.click(addButtons[0]);
    expect(screen.getByRole("button", { name: /^add meal$/i })).toBeInTheDocument();
  });

  it("submitting add entry form appends entry", async () => {
    const newEntry: MealPlanEntry = { ...entry1, id: "e2", recipeTitle: "Pasta" };
    mockInitialLoad();
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse(newEntry));

    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    const addButtons = screen.getAllByRole("button", { name: /\+ add/i });
    fireEvent.click(addButtons[0]);
    fireEvent.click(screen.getByRole("button", { name: /^add meal$/i }));

    await waitFor(() => expect(screen.getByText("Pasta")).toBeInTheDocument());
  });

  it("delete entry removes it from the calendar", async () => {
    mockInitialLoad();
    mockFetch
      .mockReturnValueOnce(jsonResponse([entry1]))
      .mockReturnValueOnce(Promise.resolve({ ok: true } as Response));

    renderPage();
    await waitFor(() => screen.getByText("Pasta"));
    fireEvent.click(screen.getByTitle("Remove"));

    await waitFor(() => expect(screen.queryByText("Pasta")).not.toBeInTheDocument());
  });

  it("clicking Share button shows SharePanel", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([]));
    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    fireEvent.click(screen.getByRole("button", { name: /^share$/i }));
    expect(screen.getByTestId("share-panel")).toBeInTheDocument();
  });

  it("prev week button navigates back a week", async () => {
    mockInitialLoad();
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))   // entries for current week
      .mockReturnValueOnce(jsonResponse([]));  // entries for prev week

    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    // Verify current week label span shows May dates
    expect(screen.getByText("4 May – 10 May 2026")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /← prev/i }));
    // Prev week (Apr 27 – May 3) should now be shown
    await waitFor(() => expect(screen.getByText("27 Apr – 3 May 2026")).toBeInTheDocument());
  });

  it("today button navigates to current week", async () => {
    mockInitialLoad();
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([]));

    renderPage();
    await waitFor(() => screen.getByText("Week 1"));
    fireEvent.click(screen.getByRole("button", { name: /← prev/i }));
    await waitFor(() => expect(mockFetch).toHaveBeenCalledTimes(5));
    fireEvent.click(screen.getByRole("button", { name: /today/i }));
    await waitFor(() => expect(mockFetch).toHaveBeenCalledTimes(6));
  });

  it("→ Add to list button shows list selector for an entry", async () => {
    mockInitialLoad();
    mockFetch.mockReturnValueOnce(jsonResponse([entry1]));

    renderPage();
    await waitFor(() => screen.getByText("Pasta"));
    fireEvent.click(screen.getByTitle(/add ingredients to shopping list/i));
    expect(screen.getByRole("option", { name: "Groceries" })).toBeInTheDocument();
  });

  it("Add to list shows success message on ok response", async () => {
    mockInitialLoad();
    mockFetch
      .mockReturnValueOnce(jsonResponse([entry1]))
      .mockReturnValueOnce(jsonResponse({ addedCount: 3 }));

    renderPage();
    await waitFor(() => screen.getByText("Pasta"));
    fireEvent.click(screen.getByTitle(/add ingredients to shopping list/i));
    fireEvent.click(screen.getByRole("button", { name: /^→$/ }));

    await waitFor(() => expect(screen.getByText(/added 3 ingredients/i)).toBeInTheDocument());
  });
});
