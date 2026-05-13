import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { authenticatedFetch } from "../components/HttpHelper";
import FamiliesPage from "./FamiliesPage";
import type { AuthState } from "../store/authStore";
import type { Family } from "../types";

vi.mock("../components/HttpHelper");
let mockStoreState: AuthState;
vi.mock("../store/authStore", () => ({
  default: vi.fn(() => mockStoreState),
}));

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

const ownerMember = { id: "m1", userId: "owner-id", email: "owner@test.com", role: "owner", joinedAt: "", isOwner: true };
const otherMember = { id: "m2", userId: "other-id", email: "other@test.com", role: "member", joinedAt: "", isOwner: false };
const family1: Family = { id: "fam-1", name: "Smith Family", ownerId: "owner-id", members: [ownerMember, otherMember] };

const baseAuth: AuthState = {
  isAuthenticated: true,
  isAdmin: false,
  user: { userId: "owner-id", email: "owner@test.com", roles: [] },
  isLoading: false,
  checkAuthStatus: vi.fn(),
  logout: vi.fn(),
};

function renderPage() {
  return render(<MemoryRouter><FamiliesPage /></MemoryRouter>);
}

describe("FamiliesPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.spyOn(window, "alert").mockImplementation(() => {});
    mockStoreState = { ...baseAuth };
  });

  it("shows loading then renders families", async () => {
    mockFetch.mockReturnValue(jsonResponse([family1]));
    renderPage();
    await waitFor(() => expect(screen.getByText("Smith Family")).toBeInTheDocument());
  });

  it("shows 'no families' message when list is empty", async () => {
    mockFetch.mockReturnValue(jsonResponse([]));
    renderPage();
    await waitFor(() => expect(screen.getByText(/no families yet/i)).toBeInTheDocument());
  });

  it("shows error when fetch fails", async () => {
    mockFetch.mockRejectedValue(new Error("fail"));
    renderPage();
    await waitFor(() => expect(screen.getByText(/failed to load families/i)).toBeInTheDocument());
  });

  it("renders member list including owner badge", async () => {
    mockFetch.mockReturnValue(jsonResponse([family1]));
    renderPage();
    await waitFor(() => screen.getByText("Smith Family"));
    expect(screen.getByText(/owner@test.com/)).toBeInTheDocument();
    expect(screen.getByText(/\(Owner\)/)).toBeInTheDocument();
  });

  it("shows Delete Family button for owner", async () => {
    mockFetch.mockReturnValue(jsonResponse([family1]));
    renderPage();
    await waitFor(() => expect(screen.getByRole("button", { name: /delete family/i })).toBeInTheDocument());
  });

  it("hides Delete Family button for non-owner", async () => {
    mockStoreState = { ...baseAuth, user: { userId: "other-id", email: "other@test.com", roles: [] } };
    mockFetch.mockReturnValue(jsonResponse([family1]));
    renderPage();
    await waitFor(() => screen.getByText("Smith Family"));
    expect(screen.queryByRole("button", { name: /delete family/i })).not.toBeInTheDocument();
  });

  it("delete family removes it from list", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(Promise.resolve({ ok: true, status: 204 } as Response));

    renderPage();
    await waitFor(() => screen.getByText("Smith Family"));
    fireEvent.click(screen.getByRole("button", { name: /delete family/i }));

    await waitFor(() => expect(screen.queryByText("Smith Family")).not.toBeInTheDocument());
  });

  it("create family form submits and adds family", async () => {
    const newFamily: Family = { id: "fam-2", name: "Jones Family", ownerId: "owner-id", members: [ownerMember] };
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse(newFamily));

    renderPage();
    await waitFor(() => screen.getByText(/no families yet/i));

    fireEvent.change(screen.getByPlaceholderText(/new family name/i), {
      target: { value: "Jones Family" },
    });
    fireEvent.click(screen.getByRole("button", { name: /create family/i }));

    await waitFor(() => expect(screen.getByText("Jones Family")).toBeInTheDocument());
  });

  it("shows error when create family fails", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse({}, false, 500));

    renderPage();
    await waitFor(() => screen.getByText(/no families yet/i));

    fireEvent.change(screen.getByPlaceholderText(/new family name/i), {
      target: { value: "Bad Family" },
    });
    fireEvent.click(screen.getByRole("button", { name: /create family/i }));

    await waitFor(() => expect(screen.getByText(/failed to create family/i)).toBeInTheDocument());
  });

  it("add member by email resolves lookup then posts", async () => {
    const newMember = { id: "m3", userId: "new-id", email: "new@test.com", role: "member", joinedAt: "", isOwner: false };
    mockFetch
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(jsonResponse({ userId: "new-id", email: "new@test.com" }))
      .mockReturnValueOnce(jsonResponse(newMember));

    renderPage();
    await waitFor(() => screen.getByText("Smith Family"));

    fireEvent.change(screen.getByPlaceholderText(/member@example.com/i), {
      target: { value: "new@test.com" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    await waitFor(() => expect(screen.getByText(/new@test.com/)).toBeInTheDocument());
  });

  it("shows error when email lookup returns 404", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(jsonResponse({}, false, 404));

    renderPage();
    await waitFor(() => screen.getByText("Smith Family"));

    fireEvent.change(screen.getByPlaceholderText(/member@example.com/i), {
      target: { value: "unknown@test.com" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    await waitFor(() => expect(screen.getByText(/no account found with that email/i)).toBeInTheDocument());
  });

  it("shows conflict error when member already exists", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(jsonResponse({ userId: "other-id" }))
      .mockReturnValueOnce(jsonResponse({}, false, 409));

    renderPage();
    await waitFor(() => screen.getByText("Smith Family"));

    fireEvent.change(screen.getByPlaceholderText(/member@example.com/i), {
      target: { value: "other@test.com" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    await waitFor(() => expect(screen.getByText(/already a member/i)).toBeInTheDocument());
  });

  it("remove member calls DELETE and removes from list", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(Promise.resolve({ ok: true, status: 204 } as Response));

    renderPage();
    await waitFor(() => screen.getByText("other@test.com"));
    fireEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(screen.queryByText("other@test.com")).not.toBeInTheDocument());
    expect(mockFetch).toHaveBeenCalledWith("/api/familymembers/m2", { method: "DELETE" });
  });
});
