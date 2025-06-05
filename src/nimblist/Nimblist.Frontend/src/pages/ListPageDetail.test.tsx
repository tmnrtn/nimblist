import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, vi, expect, beforeEach } from "vitest"; // Added beforeEach
import { MemoryRouter, Routes, Route } from "react-router-dom"; // Import Routes/Route
import type { MockedFunction } from 'vitest'; // Import Vitest helper type

// Import the function to be mocked
import { authenticatedFetch } from "../components/HttpHelper";
// Import the component under test
import ListPageDetail from "./ListPageDetail";
// Import types if needed for mock state
import type { AuthState } from "../store/authStore";
import type { ShoppingList } from "../types"; // Import ShoppingList type

// --- Mock Dependencies ---

// 1. Mock the HttpHelper module
vi.mock("../components/HttpHelper");

// 2. Mock the authStore module (using factory + variable)
let mockStoreState: AuthState;
vi.mock("../store/authStore", () => ({
  default: vi.fn(() => mockStoreState),
}));

// --- Test Suite ---
describe("ListPageDetail Component", () => {
  // --- Cast the mocked fetch function ---
  const mockAuthFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

  // --- Define reusable mock data ---
  const mockListData: ShoppingList = {
    id: "list-123", // Consistent ID matching the route
    name: "Test List",
    items: [{
      id: "item1", name: "Item 1", quantity: "2", isChecked: false,
      addedAt: "",
      shoppingListId: "list-123",
      categoryName: "",
      subCategoryName: ""
    }],
    createdAt: "",
    userId: "user-xyz"
  };

  // --- Reset mocks and setup default state before each test ---
  beforeEach(() => {
    // Reset Vitest mocks (clears calls, implementations, etc.)
    vi.clearAllMocks(); // Clears all mocks defined with vi.mock/vi.fn etc.
    // Or reset specific mocks: mockAuthFetch.mockReset();

    // Set default AUTH state (e.g., authenticated)
    mockStoreState = {
      isAuthenticated: true, // Assume logged in for detail page tests
      user: { userId: "user-xyz", email: "test@test.com" },
      isLoading: false,
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };
  });

  // --- Helper function for rendering with Router context ---
  // This sets up the route correctly for useParams
  const renderComponent = (listId: string = "list-123") => {
    return render(
      <MemoryRouter initialEntries={[`/lists/${listId}`]}> {/* Set initial route */}
        <Routes> {/* Define the route structure */}
          <Route path="/lists/:listId" element={<ListPageDetail />} />
        </Routes>
      </MemoryRouter>
    );
  };


  // --- Test Cases ---

  it("should fetch and display the shopping list details successfully", async () => {
    // Arrange: Configure mock API response for THIS test
    const mockSuccessResponse = {
      ok: true,
      status: 200,
      json: async () => mockListData, // Return our mock list data
    } as Response;
    mockAuthFetch.mockResolvedValue(mockSuccessResponse); // Use mockResolvedValue

    // Act: Render the component with routing context
    renderComponent("list-123");

    // Assert: Check fetch call
    await waitFor(() => { // Wait for fetch to be called
        expect(mockAuthFetch).toHaveBeenCalledTimes(1);
        expect(mockAuthFetch).toHaveBeenCalledWith('/api/shoppinglists/list-123', expect.any(Object));
    });

    // Assert: Check if list items are displayed
    await waitFor(() => { // Wait for list item to appear after fetch
        expect(screen.getByText(mockListData.items[0].name)).toBeInTheDocument();
    });
    // Check that no error message is shown
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it("should display an error message if the user is not authenticated (API 401)", async () => {
    // Arrange: API returns 401
    const mockUnauthorizedResponse = {
      ok: false,
      status: 401,
      json: async () => ({}), // Mock json if component tries to read it
      text: async () => 'Unauthorized', // Mock text if component tries to read it
    } as Response;
    mockAuthFetch.mockResolvedValue(mockUnauthorizedResponse);

    // Act
    renderComponent("list-123");

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        "Authentication error. Please log in again."
      );
    });
  });

   it("should display an error message if the shopping list is not found (API 404)", async () => {
    // Arrange: API returns 404
    const mockNotFoundResponse = {
      ok: false,
      status: 404,
      json: async () => ({}),
      text: async () => 'Not Found',
    } as Response;
    mockAuthFetch.mockResolvedValue(mockNotFoundResponse);

    // Act
    renderComponent("list-not-found"); // Use a different ID if needed

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        "Shopping list not found or you don't have permission."
      );
    });
  });

   it("should display a network error message if the fetch fails", async () => {
    // Arrange: Simulate network error by rejecting the promise
     const networkError = new Error("Simulated Network Error");
     mockAuthFetch.mockRejectedValue(networkError); // Use mockRejectedValue

    // Act
    renderComponent("list-123");

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        "Network error fetching list details."
      );
    });
  });

   it("should display a generic error message for other HTTP errors (API 500)", async () => {
    // Arrange: API returns 500
     const mockServerErrorResponse = {
      ok: false,
      status: 500,
      json: async () => ({}),
      text: async () => 'Server Error',
    } as Response;
    mockAuthFetch.mockResolvedValue(mockServerErrorResponse);

    // Act
    renderComponent("list-123");

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        "Failed to load list details. Status: 500"
      );
    });
  });

  it("should display specific error if not authenticated via store state", () => {
      // Arrange: Override auth store state for THIS test
      mockStoreState = {
          ...mockStoreState,
          isAuthenticated: false,
          user: null,
      };

      // Act: Render (API call won't happen due to auth check)
      renderComponent("list-123");

      // Assert: Check for the specific error message from the effect hook
      expect(screen.getByRole('alert')).toHaveTextContent(
          "Please log in to view this list."
      );
      // Ensure fetch wasn't called because isAuthenticated was false
      expect(mockAuthFetch).not.toHaveBeenCalled();
  });

});