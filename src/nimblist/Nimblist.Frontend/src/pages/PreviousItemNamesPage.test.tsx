import { render, screen, waitFor, fireEvent, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { authenticatedFetch } from "../components/HttpHelper";
import PreviousItemNamesPage from "./PreviousItemNamesPage";

vi.mock("../components/HttpHelper");

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

function noBodyResponse(ok = true, status = 204) {
  return Promise.resolve({ ok, status } as Response);
}

function renderPage() {
  return render(<PreviousItemNamesPage />);
}

describe("PreviousItemNamesPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.spyOn(window, "alert").mockImplementation(() => {});
  });

  it("shows loading state initially", () => {
    // Return a promise that never resolves so we stay in loading state
    mockFetch.mockReturnValueOnce(new Promise(() => {}));
    renderPage();
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("renders sorted list of names", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse(["Milk", "Apples", "Bread"]));
    renderPage();
    await waitFor(() => expect(screen.getByText("Apples")).toBeInTheDocument());
    expect(screen.getByText("Bread")).toBeInTheDocument();
    expect(screen.getByText("Milk")).toBeInTheDocument();

    // Verify alphabetical order by checking DOM position
    const items = screen.getAllByRole("listitem");
    const texts = items.map((li) => li.textContent?.trim().replace("Delete", "").trim());
    expect(texts).toEqual(["Apples", "Bread", "Milk"]);
  });

  it("shows empty message when no names returned", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse([]));
    renderPage();
    await waitFor(() =>
      expect(screen.getByText("No previous item names found.")).toBeInTheDocument(),
    );
  });

  it("shows error when fetch fails", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse(null, false, 500));
    renderPage();
    await waitFor(() =>
      expect(screen.getByText("Failed to load previous item names.")).toBeInTheDocument(),
    );
  });

  it("delete name calls DELETE after confirm and removes from list", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse(["Milk", "Bread"]))  // initial load
      .mockReturnValueOnce(noBodyResponse(true, 204));        // DELETE

    renderPage();
    await waitFor(() => screen.getByText("Milk"));

    // Click Delete on Bread (first alphabetically)
    const deleteButtons = screen.getAllByRole("button", { name: "Delete" });
    // Items sorted: Bread, Milk — first button is Bread's Delete
    fireEvent.click(deleteButtons[0]);

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        `/api/PreviousItemNames/${encodeURIComponent("Bread")}`,
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
    await waitFor(() =>
      expect(screen.queryByText("Bread")).not.toBeInTheDocument(),
    );
    // Milk should still be present
    expect(screen.getByText("Milk")).toBeInTheDocument();
  });

  it("delete name does not call DELETE when confirm is cancelled", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    mockFetch.mockReturnValueOnce(jsonResponse(["Milk"]));

    renderPage();
    await waitFor(() => screen.getByText("Milk"));
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    // Only the initial load — no DELETE call
    expect(mockFetch).toHaveBeenCalledTimes(1);
    expect(screen.getByText("Milk")).toBeInTheDocument();
  });

  it("shows Deleting... on button while delete is in progress", async () => {
    let resolveDelete!: (value: Response) => void;
    const deletePromise = new Promise<Response>((resolve) => {
      resolveDelete = resolve;
    });

    mockFetch
      .mockReturnValueOnce(jsonResponse(["Milk"]))  // initial load
      .mockReturnValueOnce(deletePromise);           // DELETE — held open

    renderPage();
    await waitFor(() => screen.getByText("Milk"));

    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    // While delete is in flight, button should show "Deleting..."
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Deleting..." })).toBeInTheDocument(),
    );

    // Resolve the delete so the component can finish
    await act(async () => {
      resolveDelete({ ok: true, status: 204 } as Response);
    });

    await waitFor(() =>
      expect(screen.queryByText("Milk")).not.toBeInTheDocument(),
    );
  });
});
