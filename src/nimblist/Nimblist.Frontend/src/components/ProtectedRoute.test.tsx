import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import type { AuthState } from "../store/authStore";
import ProtectedRoute from "./ProtectedRoute";

let mockStoreState: AuthState;
vi.mock("../store/authStore", () => ({
  default: vi.fn(() => mockStoreState),
}));

vi.mock("./LoginPrompt", () => ({
  default: () => <div>Please log in</div>,
}));

const base: AuthState = {
  isAuthenticated: false,
  isAdmin: false,
  isPaid: false,
  user: null,
  isLoading: false,
  checkAuthStatus: vi.fn(),
  logout: vi.fn(),
};

function renderProtected() {
  return render(
    <MemoryRouter initialEntries={["/protected"]}>
      <Routes>
        <Route element={<ProtectedRoute />}>
          <Route path="/protected" element={<div>Protected content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe("ProtectedRoute", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockStoreState = { ...base };
  });

  it("shows loading indicator while auth is being checked", () => {
    mockStoreState = { ...base, isLoading: true };
    renderProtected();
    expect(screen.getByText("Checking authentication...")).toBeInTheDocument();
    expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
  });

  it("renders outlet when authenticated", () => {
    mockStoreState = { ...base, isAuthenticated: true, user: { userId: "u1", email: "u@test.com", roles: [], subscriptionTier: 'free', isInTrial: false, trialEndDate: null } };
    renderProtected();
    expect(screen.getByText("Protected content")).toBeInTheDocument();
    expect(screen.queryByText("Please log in")).not.toBeInTheDocument();
  });

  it("renders login prompt when not authenticated", () => {
    mockStoreState = { ...base, isAuthenticated: false };
    renderProtected();
    expect(screen.getByText("Please log in")).toBeInTheDocument();
    expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
  });
});
