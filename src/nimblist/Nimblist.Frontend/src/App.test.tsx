import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, vi, expect } from "vitest";
import type { MockedFunction } from 'vitest';
import { MemoryRouter } from "react-router-dom";

vi.mock('./components/HttpHelper');

import App from "./App";
import type { AuthState } from "./store/authStore";
import { authenticatedFetch } from "./components/HttpHelper";
import { ShoppingList } from './types'; // Import the interface (adjust path if needed)


let mockStoreState: AuthState = {
  isAuthenticated: false,
  user: null,
  isLoading: true, // Default to loading, perhaps
  checkAuthStatus: vi.fn().mockResolvedValue(undefined),
  logout: vi.fn().mockResolvedValue(undefined),
};

vi.mock("./store/authStore", () => ({
// The factory needs to provide the default export
default: vi.fn(() => mockStoreState), // Mock the hook call to return our variable

}));
  
vi.mock("./hooks/useShoppingListHub", () => ({
  __esModule: true,
  default: vi.fn(() => ({
    connection: null,
    isConnected: true,
    joinGroup: vi.fn(),
    leaveGroup: vi.fn(),
  })),
}));

describe("App Component", () => {
  const mockAuthFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

  it("renders the loading state when isLoading is true", () => {
    mockStoreState = {
      isAuthenticated: false,
      user: null,
      isLoading: true, // Default to loading, perhaps
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };

    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>
    );

    expect(screen.getByText("Loading Application...")).toBeInTheDocument();
  });

  it("renders the Layout and HomePage when isLoading is false", async () => {
    mockStoreState = {
      isAuthenticated: false,
      user: null,
      isLoading: false, // Default to loading, perhaps
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };
    render(
      <MemoryRouter initialEntries={["/"]}>
        <App />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Home Page")).toBeInTheDocument(); // Assuming HomePage renders "HomePage"
    });
  });

  it("renders the ShoppingListsPage when navigating to /lists", async () => {
    mockStoreState = {
      isAuthenticated: true,
      user: {
        userId: "123",
        email: "test@test.com",
      },
      isLoading: false, // Default to loading, perhaps
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };

    const mockData: ShoppingList[] = [
      {
        id: "1",
        name: "Groceries",
        items: [],
        createdAt: "2023-01-01T00:00:00Z",
        userId: "123",
      },
      {
        id: "2",
        name: "Hardware",
        items: [],
        createdAt: "2023-01-01T00:00:00Z",
        userId: "123",
      },
    ];

    const mockSuccessResponse = {
      ok: true,
      status: 200,
      json: async () => mockData, // If component calls response.json()
      text: async () => JSON.stringify(mockData), // If component calls response.text()
    } as Response;

    mockAuthFetch.mockResolvedValue(mockSuccessResponse);

    render(
      <MemoryRouter initialEntries={["/lists"]}>
        <App />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Create New List")).toBeInTheDocument(); // Assuming ShoppingListsPage renders "ShoppingListsPage"
    });
  });

  it("renders the ListPageDetail when navigating to /lists/:listId", async () => {
    mockStoreState = {
        isAuthenticated: true,
        user: {
          userId: "123",
          email: "test@test.com",
        },
        isLoading: false, // Default to loading, perhaps
        checkAuthStatus: vi.fn().mockResolvedValue(undefined),
        logout: vi.fn().mockResolvedValue(undefined),
      };
  
      const mockData: ShoppingList[] = [
        {
          id: "1",
          name: "Groceries",
          items: [],
          createdAt: "2023-01-01T00:00:00Z",
          userId: "123",
        },
        {
          id: "2",
          name: "Hardware",
          items: [],
          createdAt: "2023-01-01T00:00:00Z",
          userId: "123",
        },
      ];
  
      const mockSuccessResponse = {
        ok: true,
        status: 200,
        json: async () => mockData, // If component calls response.json()
        text: async () => JSON.stringify(mockData), // If component calls response.text()
      } as Response;
  
      mockAuthFetch.mockResolvedValue(mockSuccessResponse);

    render(
      <MemoryRouter initialEntries={["/lists/1"]}>
        <App />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Add New Item")).toBeInTheDocument(); // Assuming ListPageDetail renders "ListPageDetail"
    });
  });

  it("renders the NotFoundPage for unmatched routes", async () => {
    mockStoreState = {
        isAuthenticated: true,
        user: {
          userId: "123",
          email: "test@test.com",
        },
        isLoading: false, // Default to loading, perhaps
        checkAuthStatus: vi.fn().mockResolvedValue(undefined),
        logout: vi.fn().mockResolvedValue(undefined),
      };

    render(
      <MemoryRouter initialEntries={["/non-existent-route"]}>
        <App />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Sorry, the page you were looking for could not be found.")).toBeInTheDocument(); // Assuming NotFoundPage renders "NotFoundPage"
    });
  });

  it("calls checkAuthStatus on mount", () => {
    mockStoreState = {
        isAuthenticated: false,
        user: null,
        isLoading: false, // Default to loading, perhaps
        checkAuthStatus: vi.fn().mockResolvedValue(undefined),
        logout: vi.fn().mockResolvedValue(undefined),
      };

    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>
    );

    expect(mockStoreState.checkAuthStatus).toHaveBeenCalledTimes(1);
  });
});
