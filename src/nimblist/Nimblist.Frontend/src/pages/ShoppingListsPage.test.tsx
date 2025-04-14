// src/pages/ShoppingListsPage.test.tsx
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { MemoryRouter } from "react-router-dom"; // Needed because component uses <Link>
import type { MockedFunction } from "vitest";

// Import types and component
import type { AuthState } from "../store/authStore";
import type { ShoppingList } from "../types";
import { authenticatedFetch } from "../components/HttpHelper"; // Function to mock
import ShoppingListsPage from "./ShoppingListsPage"; // Component under test

// --- Mock Dependencies ---

// 1. Mock authStore module
let mockStoreState: AuthState;
vi.mock("../store/authStore", () => ({
  default: vi.fn(() => mockStoreState),
}));

// 2. Mock HttpHelper module (where authenticatedFetch resides)
vi.mock("../components/HttpHelper");

// --- Test Suite ---
describe("ShoppingListsPage Component", () => {
  // Cast the mocked fetch function for type safety
  const mockAuthFetch = authenticatedFetch as MockedFunction<
    typeof authenticatedFetch
  >;

  // Define mock list data for successful responses
  const mockListsData: ShoppingList[] = [
    {
      id: "list-1",
      name: "Groceries",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
    },
    {
      id: "list-2",
      name: "Hardware Store",
      createdAt: "2025-04-03T11:00:00Z",
      items: [],
      userId: "test-user-id",
    },
  ];

  beforeEach(() => {
    // Reset all mocks (clears calls, implementations, etc.) before each test
    vi.clearAllMocks();

    // Set default mock auth state: Authenticated, not loading
    mockStoreState = {
      isAuthenticated: true,
      user: { userId: "test-user-id", email: "test@example.com" },
      isLoading: false,
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };

    // Explicitly reset the fetch mock (optional if clearAllMocks is used, but good practice)
    mockAuthFetch.mockReset();
  });

  it("should display login prompt error if user is not authenticated", () => {
    // Arrange: Override auth state for this test
    mockStoreState = { ...mockStoreState, isAuthenticated: false, user: null };

    // Act: Render the component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Assert: Check for the specific error message set when not authenticated
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Please log in to view your shopping lists."
    );
    // Assert: Fetch should NOT have been called
    expect(mockAuthFetch).not.toHaveBeenCalled();
  });

  it("should display loading indicator while fetching data", () => {
    // Arrange: Make the fetch promise never resolve to keep it loading
    mockAuthFetch.mockImplementation(() => new Promise(() => {}));

    // Act
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Assert: Check for loading text
    expect(screen.getByText("Loading lists...")).toBeInTheDocument();
    // Assert: Fetch *should* have been called (since user is authenticated by default beforeEach)
    expect(mockAuthFetch).toHaveBeenCalledTimes(1);
    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppinglists",
      expect.any(Object)
    );
  });

  it("should fetch and display shopping lists successfully", async () => {
    // Arrange: Mock a successful API response
    const mockSuccessResponse = {
      ok: true,
      status: 200,
      json: async () => mockListsData, // Return the mock data
    } as Response;
    mockAuthFetch.mockResolvedValue(mockSuccessResponse);

    // Act
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Assert: Wait for loading to finish and lists to appear
    // Use findBy* which includes waitFor
    expect(await screen.findByText(mockListsData[0].name)).toBeInTheDocument();
    expect(screen.getByText(mockListsData[1].name)).toBeInTheDocument();

    // Check that loading/error states are not present
    expect(screen.queryByText("Loading lists...")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    // Check if links are rendered correctly
    const link1 = screen.getByRole("link", { name: mockListsData[0].name });
    expect(link1).toHaveAttribute("href", `/lists/${mockListsData[0].id}`);
    const link2 = screen.getByRole("link", { name: mockListsData[1].name });
    expect(link2).toHaveAttribute("href", `/lists/${mockListsData[1].id}`);

    // Verify fetch call
    expect(mockAuthFetch).toHaveBeenCalledTimes(1);
    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppinglists",
      expect.objectContaining({ method: "GET" })
    );
  });


  it("should display specific error message on API 401 Unauthorized", async () => {
    // Arrange: Mock 401 response
    const mockUnauthorizedResponse = {
      ok: false,
      status: 401,
      text: async () => "Unauthorized", // Mock methods component might use
      json: async () => ({ message: "Unauthorized access" }),
    } as Response;
    mockAuthFetch.mockResolvedValue(mockUnauthorizedResponse);

    // Act
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Assert: Check for the specific 401 error message
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Your session may have expired. Please log out and log back in."
    );
  });

  it("should display generic error message on other API errors (e.g., 500)", async () => {
    // Arrange: Mock 500 response
    const mockServerErrResponse = {
      ok: false,
      status: 500,
      statusText: "Internal Server Error",
      text: async () => "Server exploded",
      json: async () => ({}),
    } as Response;
    mockAuthFetch.mockResolvedValue(mockServerErrResponse);

    // Act
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Assert: Check for the generic error message including status code
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Failed to load lists. Server responded with 500."
    );
  });

  it("should display network error message if fetch itself rejects", async () => {
    // Arrange: Mock fetch rejection
    const networkError = new Error("Failed to fetch");
    mockAuthFetch.mockRejectedValue(networkError);

    // Act
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Assert: Check for the network error message handled by the catch block
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Unable to connect to the server. Please check your network connection."
    );
  });

  // Tests for handleAddList functionality
  it("should toggle the form visibility when handleClickNew is called", async () => {
    // Arrange: Setup successful API response first to get past the loading state
    mockAuthFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load (to get past loading state)
    await screen.findByText(mockListsData[0].name);

    // Form should initially be hidden
    expect(screen.queryByText("Add New List")).not.toBeInTheDocument();

    // Act: Click the create new list button
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Assert: Form should now be visible
    expect(screen.getByText("Add New List")).toBeInTheDocument();

    // Act: Click button again to hide
    fireEvent.click(createButton);

    // Assert: Form should be hidden again
    expect(screen.queryByText("Add New List")).not.toBeInTheDocument();
  });

  it("should successfully add a new shopping list", async () => {
    // Arrange: Mock successful API responses
    // First for the initial lists fetch
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Then for the add list operation
    const newList: ShoppingList = {
      id: "new-list-id",
      name: "New Test List",
      createdAt: "2025-05-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: async () => newList,
    } as Response);

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load
    await screen.findByText(mockListsData[0].name);

    // Click to show the form
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Fill in the form
    const input = screen.getByLabelText("New list name");
    fireEvent.change(input, { target: { value: "New Test List" } });

    // Submit the form
    const submitButton = screen.getByRole("button", { name: "Add Item" });
    fireEvent.click(submitButton);

    // Assert: Check if the API was called correctly
    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppingLists",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ name: "New Test List" }),
      })
    );

    // Assert: New list should be in the DOM after adding
    await screen.findByText(newList.name);

    // The form should be hidden after successful submission
    expect(screen.queryByText("Add New List")).not.toBeInTheDocument();
  });

  it("should display an error when adding a list fails with validation error (400)", async () => {
    // Arrange: Mock initial list fetch success
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Mock validation error response
    mockAuthFetch.mockResolvedValueOnce({
      ok: false,
      status: 400,
      statusText: "Bad Request",
      text: async () => "List name is required",
    } as Response);

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load
    await screen.findByText(mockListsData[0].name);

    // Click to show the form
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Fill in the form
    const input = screen.getByLabelText("New list name");
    fireEvent.change(input, { target: { value: "Invalid List" } });

    // Submit the form
    const submitButton = screen.getByRole("button", { name: "Add Item" });
    fireEvent.click(submitButton);

    // Assert: Check if error message is displayed
    expect(
      await screen.findByText("Failed to add item. Please check your input.")
    ).toBeInTheDocument();
  });

  it("should display an error when adding a list fails with server error (500)", async () => {
    // Arrange: Mock initial list fetch success
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Mock server error response
    mockAuthFetch.mockResolvedValueOnce({
      ok: false,
      status: 500,
      statusText: "Internal Server Error",
      text: async () => "Server error",
    } as Response);

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load
    await screen.findByText(mockListsData[0].name);

    // Click to show the form
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Fill in the form
    const input = screen.getByLabelText("New list name");
    fireEvent.change(input, { target: { value: "New List" } });

    // Submit the form
    const submitButton = screen.getByRole("button", { name: "Add Item" });
    fireEvent.click(submitButton);

    // Assert: Check if error message is displayed
    expect(
      await screen.findByText("Failed to add item. Please try again later.")
    ).toBeInTheDocument();
  });

  it("should display an error when adding a list fails with network error", async () => {
    // Arrange: Mock initial list fetch success
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Mock network error
    mockAuthFetch.mockRejectedValueOnce(new Error("Network failure"));

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load
    await screen.findByText(mockListsData[0].name);

    // Click to show the form
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Fill in the form
    const input = screen.getByLabelText("New list name");
    fireEvent.change(input, { target: { value: "New List" } });

    // Submit the form
    const submitButton = screen.getByRole("button", { name: "Add Item" });
    fireEvent.click(submitButton);

    // Assert: Check if error message is displayed
    expect(
      await screen.findByText("Failed to connect to the server to add item.")
    ).toBeInTheDocument();
  });

  it("should disable the submit button when the name field is empty", async () => {
    // Arrange: Mock successful API response
    mockAuthFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load
    await screen.findByText(mockListsData[0].name);

    // Click to show the form
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Assert: Submit button should be disabled with empty input
    const input = screen.getByLabelText("New list name");
    expect(input).toHaveValue("");

    const submitButton = screen.getByRole("button", { name: "Add Item" });
    expect(submitButton).toBeDisabled();

    // Act: Type something into the input
    fireEvent.change(input, { target: { value: "New List" } });

    // Assert: Button should be enabled
    expect(submitButton).not.toBeDisabled();

    // Act: Clear the input
    fireEvent.change(input, { target: { value: "" } });

    // Assert: Button should be disabled again
    expect(submitButton).toBeDisabled();
  });

  it('should display "Adding..." text on button when submission is in progress', async () => {
    // Arrange: Mock successful API response for initial lists
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockListsData,
    } as Response);

    // Create a promise we can resolve manually to control timing
    let resolveAddPromise: (value: unknown) => void = () => {};
    const addPromise = new Promise((resolve) => {
      resolveAddPromise = resolve;
    });

    // Mock the add list API call to return our controlled promise
    mockAuthFetch.mockImplementationOnce((_url, options) => {
      if (options?.method === "POST") {
        return addPromise as Promise<Response>;
      }
      return Promise.resolve({
        ok: true,
        status: 200,
        json: async () => mockListsData,
      } as Response);
    });

    // Act: Render component
    render(
      <MemoryRouter>
        <ShoppingListsPage />
      </MemoryRouter>
    );

    // Wait for lists to load
    await screen.findByText(mockListsData[0].name);

    // Click to show the form
    const createButton = screen.getByRole("button", {
      name: "Create New List",
    });
    fireEvent.click(createButton);

    // Fill in the form
    const input = screen.getByLabelText("New list name");
    fireEvent.change(input, { target: { value: "New List" } });

    // Submit the form but don't resolve the promise yet
    const submitButton = screen.getByRole("button", { name: "Add Item" });
    fireEvent.click(submitButton);

    // Assert: Button should show loading state
    expect(
      await screen.findByRole("button", { name: "Adding..." })
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Adding..." })).toBeDisabled();

    // Cleanup: Resolve the promise so the test doesn't hang
    // Fix: Create a properly structured Response mock
    const newList = {
      id: "new-id",
      name: "New List",
      createdAt: new Date().toISOString(),
      items: [],
      userId: "test-user-id",
    };
    
    const mockResponseBody = JSON.stringify(newList);
    const mockResponse = new Response(mockResponseBody, {
      status: 201,
      statusText: "Created",
      headers: {
        'Content-Type': 'application/json'
      }
    });

    // Override the json method to return our newList object
    Object.defineProperty(mockResponse, 'json', {
      writable: true,
      value: async () => newList
    });
    
    // Resolve the promise with our properly constructed Response
    resolveAddPromise(mockResponse);
    
    // Wait for state update after promise resolves
    await screen.findByText("New List");
  });
});
