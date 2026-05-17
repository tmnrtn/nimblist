import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import type { AuthState } from "../store/authStore";
import AdminRoute from "./AdminRoute";

let mockStoreState: AuthState;
vi.mock("../store/authStore", () => ({
  default: vi.fn(() => mockStoreState),
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

function renderAdminRoute() {
  return render(
    <MemoryRouter initialEntries={["/"]}>
      <Routes>
        <Route element={<AdminRoute />}>
          <Route path="/" element={<div>Admin content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe("AdminRoute", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockStoreState = { ...base };
  });

  it("shows loading state when isLoading is true", () => {
    mockStoreState = { ...base, isLoading: true };
    renderAdminRoute();
    expect(screen.getByText("Checking authentication...")).toBeInTheDocument();
    expect(screen.queryByText("Admin content")).not.toBeInTheDocument();
  });

  it("shows login prompt when not authenticated", () => {
    mockStoreState = { ...base, isAuthenticated: false };
    renderAdminRoute();
    expect(
      screen.getByText(/please log in to access this page/i)
    ).toBeInTheDocument();
    expect(screen.queryByText("Admin content")).not.toBeInTheDocument();
  });

  it("shows access denied when authenticated but not admin", () => {
    mockStoreState = {
      ...base,
      isAuthenticated: true,
      isAdmin: false,
  isPaid: false,
      user: { userId: "u1", email: "user@test.com", roles: ["Standard"], subscriptionTier: 'free', isInTrial: false, trialEndDate: null },
    };
    renderAdminRoute();
    expect(
      screen.getByText(/access denied/i)
    ).toBeInTheDocument();
    expect(
      screen.getByText(/admin privileges required/i)
    ).toBeInTheDocument();
    expect(screen.queryByText("Admin content")).not.toBeInTheDocument();
  });

  it("renders Outlet when authenticated and admin", () => {
    mockStoreState = {
      ...base,
      isAuthenticated: true,
      isAdmin: true,
      user: { userId: "u2", email: "admin@test.com", roles: ["Admin"], subscriptionTier: 'free', isInTrial: false, trialEndDate: null },
    };
    renderAdminRoute();
    expect(screen.getByText("Admin content")).toBeInTheDocument();
    expect(screen.queryByText("Checking authentication...")).not.toBeInTheDocument();
    expect(screen.queryByText(/please log in/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/access denied/i)).not.toBeInTheDocument();
  });
});
