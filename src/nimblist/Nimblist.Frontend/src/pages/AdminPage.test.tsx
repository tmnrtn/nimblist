import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { authenticatedFetch } from "../components/HttpHelper";
import AdminPage from "./AdminPage";

vi.mock("../components/HttpHelper");

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

function noBodyResponse(ok = true, status = 204) {
  return Promise.resolve({ ok, status } as Response);
}

const user1 = { userId: "u1", email: "alice@example.com", roles: ["Standard"] };
const user2 = { userId: "u2", email: "bob@example.com", roles: ["Admin"] };

const member1 = {
  memberId: "m1",
  userId: "u3",
  email: "carol@example.com",
  role: "Member",
  joinedAt: "2026-01-01T00:00:00Z",
};
const family1 = {
  id: "f1",
  name: "The Nortons",
  ownerUserId: "u1",
  ownerEmail: "alice@example.com",
  members: [member1],
};

const feedback1 = {
  id: "fb1",
  itemName: "Granny Smith Apple",
  categoryName: "Fruit",
  subCategoryName: "Apples",
  userEmail: "alice@example.com",
  createdAt: "2026-03-15T10:00:00Z",
};

const llmSettings = {
  provider: "openrouter",
  model: "anthropic/claude-3-haiku",
  visionModel: "",
  apiKey: "sk-or-****",
  baseUrl: "",
  imageSearchApiKey: "",
  updatedAt: "2026-04-01T12:00:00Z",
};

function renderPage() {
  return render(<AdminPage />);
}

describe("AdminPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.spyOn(window, "alert").mockImplementation(() => {});
    // Default: users tab loads on mount
    mockFetch.mockResolvedValue(
      jsonResponse([]) as unknown as Promise<Response>,
    );
  });

  it("renders tab navigation with 4 tabs", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse([]));
    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));
    expect(screen.getByRole("button", { name: "Users" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Families" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "LLM Settings" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Classification Feedback" })).toBeInTheDocument();
  });

  it("loads users on mount and displays them", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse([user1, user2]));
    renderPage();
    await waitFor(() => expect(screen.getByText("alice@example.com")).toBeInTheDocument());
    expect(screen.getByText("bob@example.com")).toBeInTheDocument();
    expect(mockFetch).toHaveBeenCalledWith("/api/admin/users");
  });

  it("shows Standard role chip and Make Admin button for Standard user", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse([user1]));
    renderPage();
    await waitFor(() => expect(screen.getByText("Standard")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Make Admin" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Make Standard" })).not.toBeInTheDocument();
  });

  it("shows Admin role chip and Make Standard button for Admin user", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse([user2]));
    renderPage();
    await waitFor(() => expect(screen.getByText("Admin")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Make Standard" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Make Admin" })).not.toBeInTheDocument();
  });

  it("set role to Admin calls PUT and updates UI", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([user1]))            // mount: load users
      .mockReturnValueOnce(noBodyResponse(true, 200));       // PUT role

    renderPage();
    await waitFor(() => screen.getByRole("button", { name: "Make Admin" }));
    fireEvent.click(screen.getByRole("button", { name: "Make Admin" }));

    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith(
      `/api/admin/users/${user1.userId}/role`,
      expect.objectContaining({ method: "PUT" }),
    ));
    // Role chip should update to Admin
    await waitFor(() => expect(screen.getByText("Admin")).toBeInTheDocument());
    expect(screen.queryByRole("button", { name: "Make Admin" })).not.toBeInTheDocument();
  });

  it("delete user calls DELETE after confirm and removes from list", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([user1, user2]))     // mount
      .mockReturnValueOnce(noBodyResponse(true, 204));       // DELETE

    renderPage();
    await waitFor(() => screen.getByText("alice@example.com"));

    // First Delete button belongs to user1 (Standard), click it
    const deleteButtons = screen.getAllByRole("button", { name: "Delete" });
    fireEvent.click(deleteButtons[0]);

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        `/api/admin/users/${user1.userId}`,
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
    await waitFor(() =>
      expect(screen.queryByText("alice@example.com")).not.toBeInTheDocument(),
    );
  });

  it("delete user does not call DELETE when confirm is cancelled", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    mockFetch.mockReturnValueOnce(jsonResponse([user1]));

    renderPage();
    await waitFor(() => screen.getByText("alice@example.com"));
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    // Only the initial load call, no DELETE
    expect(mockFetch).toHaveBeenCalledTimes(1);
    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
  });

  it("switching to Families tab loads families and shows family name", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))           // mount: users
      .mockReturnValueOnce(jsonResponse([family1]));   // families tab

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "Families" }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith("/api/admin/families"),
    );
    await waitFor(() => expect(screen.getByText("The Nortons")).toBeInTheDocument());
  });

  it("remove family member calls DELETE and removes member from UI", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))           // mount: users
      .mockReturnValueOnce(jsonResponse([family1]))    // families tab
      .mockReturnValueOnce(noBodyResponse(true, 204)); // DELETE member

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "Families" }));
    await waitFor(() => screen.getByText("carol@example.com"));

    fireEvent.click(screen.getByRole("button", { name: "Remove" }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        `/api/admin/families/${family1.id}/members/${member1.memberId}`,
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
    await waitFor(() =>
      expect(screen.queryByText("carol@example.com")).not.toBeInTheDocument(),
    );
  });

  it("delete family calls DELETE and removes family from UI", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))           // mount: users
      .mockReturnValueOnce(jsonResponse([family1]))    // families tab
      .mockReturnValueOnce(noBodyResponse(true, 204)); // DELETE family

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "Families" }));
    await waitFor(() => screen.getByText("The Nortons"));

    fireEvent.click(screen.getByRole("button", { name: "Delete Family" }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        `/api/admin/families/${family1.id}`,
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
    await waitFor(() =>
      expect(screen.queryByText("The Nortons")).not.toBeInTheDocument(),
    );
  });

  it("switching to Classification Feedback tab loads feedback records", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))             // mount: users
      .mockReturnValueOnce(jsonResponse([feedback1]));   // feedback tab

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "Classification Feedback" }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith("/api/admin/classification-feedback"),
    );
    await waitFor(() => expect(screen.getByText("Granny Smith Apple")).toBeInTheDocument());
  });

  it("shows 'No feedback records.' message when feedback list is empty", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))   // mount: users
      .mockReturnValueOnce(jsonResponse([]));  // feedback tab

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "Classification Feedback" }));

    await waitFor(() =>
      expect(screen.getByText("No feedback records.")).toBeInTheDocument(),
    );
  });

  it("delete feedback record calls DELETE and removes row from UI", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))             // mount: users
      .mockReturnValueOnce(jsonResponse([feedback1]))    // feedback tab
      .mockReturnValueOnce(noBodyResponse(true, 204));   // DELETE

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "Classification Feedback" }));
    await waitFor(() => screen.getByText("Granny Smith Apple"));

    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        `/api/admin/classification-feedback/${feedback1.id}`,
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
    await waitFor(() =>
      expect(screen.queryByText("Granny Smith Apple")).not.toBeInTheDocument(),
    );
  });

  it("switching to LLM Settings tab loads settings and shows provider in select", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))             // mount: users
      .mockReturnValueOnce(jsonResponse(llmSettings));   // llm tab

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "LLM Settings" }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith("/api/admin/llm-settings"),
    );
    await waitFor(() => {
      const select = screen.getByRole("combobox") as HTMLSelectElement;
      expect(select.value).toBe("openrouter");
    });
  });

  it("save LLM settings calls PUT with form values", async () => {
    const savedResponse = { ...llmSettings, provider: "openai", model: "gpt-4o-mini", updatedAt: "2026-05-01T00:00:00Z" };
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))              // mount: users
      .mockReturnValueOnce(jsonResponse(llmSettings))     // llm tab load
      .mockReturnValueOnce(jsonResponse(savedResponse));  // PUT

    renderPage();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledWith("/api/admin/users"));

    fireEvent.click(screen.getByRole("button", { name: "LLM Settings" }));
    await waitFor(() => screen.getByRole("combobox"));

    // Change provider
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "openai" } });

    fireEvent.click(screen.getByRole("button", { name: /save settings/i }));

    await waitFor(() =>
      expect(mockFetch).toHaveBeenCalledWith(
        "/api/admin/llm-settings",
        expect.objectContaining({ method: "PUT" }),
      ),
    );
    await waitFor(() => expect(screen.getByText("Settings saved.")).toBeInTheDocument());
  });

  it("shows error message when users load fails", async () => {
    mockFetch.mockReturnValueOnce(jsonResponse(null, false, 500));

    renderPage();

    await waitFor(() =>
      expect(screen.getByText(/failed to load users/i)).toBeInTheDocument(),
    );
  });
});
