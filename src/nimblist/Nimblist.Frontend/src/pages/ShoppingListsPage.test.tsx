// src/pages/ShoppingListsPage.test.tsx
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { MemoryRouter } from "react-router-dom"; // Needed because component uses <Link>
import type { MockedFunction } from "vitest";

// Mock SharePanel so it doesn't make its own fetch calls
vi.mock("../components/SharePanel", () => ({
  default: (props: Record<string, unknown>) => (
    <div data-testid="share-panel" data-endpoint={props.endpoint as string} data-is-owner={String(props.isOwner)} />
  ),
}));

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
      isAdmin: false,
  isPaid: false,
      user: { userId: "test-user-id", email: "test@example.com", roles: [], subscriptionTier: 'free', isInTrial: false, trialEndDate: null },
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
      "/api/shoppinglists",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ name: "New Test List", isTemplate: false }),
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

  it("shows Delete button only for owned lists", async () => {
    const sharedList: ShoppingList = { id: "list-3", name: "Shared List", createdAt: "2025-04-03T12:00:00Z", items: [], userId: "other-user-id" };
    mockAuthFetch.mockResolvedValue({ ok: true, json: async () => [...mockListsData, sharedList] } as Response);
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    // Only the two owned lists get Delete buttons
    expect(deleteButtons).toHaveLength(2);
  });

  it("delete button prompts for confirmation and removes list on confirm", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => mockListsData } as Response)
      .mockResolvedValueOnce({ ok: true, status: 204 } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    fireEvent.click(screen.getAllByRole("button", { name: /delete/i })[0]);

    await screen.findByText("Hardware Store");
    expect(screen.queryByText("Groceries")).not.toBeInTheDocument();
    expect(mockAuthFetch).toHaveBeenCalledWith("/api/shoppinglists/list-1", { method: "DELETE" });
  });

  it("delete does nothing when confirmation is cancelled", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    mockAuthFetch.mockResolvedValue({ ok: true, json: async () => mockListsData } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    fireEvent.click(screen.getAllByRole("button", { name: /delete/i })[0]);

    expect(screen.getByText("Groceries")).toBeInTheDocument();
    // DELETE was never called (only the initial GET)
    expect(mockAuthFetch).toHaveBeenCalledTimes(1);
  });

  it("restores list in state when delete request fails", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => mockListsData } as Response)
      .mockRejectedValueOnce(new Error("Network error"));

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    fireEvent.click(screen.getAllByRole("button", { name: /delete/i })[0]);

    // After network failure the list should be restored
    expect(await screen.findByText("Groceries")).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // handleToggleTemplate
  // ---------------------------------------------------------------------------

  it("toggles an active list to template when 'Make template' is clicked", async () => {
    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => mockListsData } as Response)
      .mockResolvedValueOnce({ ok: true, json: async () => ({}) } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    // Both owned active lists should show "Make template"
    const makeTemplateButtons = screen.getAllByRole("button", { name: "Make template" });
    expect(makeTemplateButtons.length).toBeGreaterThanOrEqual(1);

    fireEvent.click(makeTemplateButtons[0]);

    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppinglists/list-1",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ name: "Groceries", isTemplate: true }),
      })
    );

    // After success the list moves to Templates section
    await screen.findByText("Templates");
    expect(screen.getByText("Templates")).toBeInTheDocument();
  });

  it("toggles a template back to active list when 'Untemplate' is clicked", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-1",
      name: "Weekly Shop",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => [templateList] } as Response)
      .mockResolvedValueOnce({ ok: true, json: async () => ({}) } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Weekly Shop");

    const untemplateButton = screen.getByRole("button", { name: "Untemplate" });
    fireEvent.click(untemplateButton);

    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppinglists/tmpl-1",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ name: "Weekly Shop", isTemplate: false }),
      })
    );

    // After success the list should appear as an active list (as a link, not plain text)
    await waitFor(() => {
      expect(screen.queryByText("Templates")).not.toBeInTheDocument();
    });
  });

  it("does not update UI when toggle template API call fails", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-2",
      name: "Fail Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => [templateList] } as Response)
      .mockResolvedValueOnce({ ok: false, status: 500, json: async () => ({}) } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Fail Template");

    fireEvent.click(screen.getByRole("button", { name: "Untemplate" }));

    // Remains in templates section
    await waitFor(() => {
      expect(screen.getByText("Templates")).toBeInTheDocument();
    });
  });

  // ---------------------------------------------------------------------------
  // Template rendering
  // ---------------------------------------------------------------------------

  it("renders template lists in a separate Templates section", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-3",
      name: "Party Snacks",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [...mockListsData, templateList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    await screen.findByText("Party Snacks");
    expect(screen.getByText("Templates")).toBeInTheDocument();
    // Template name is plain text, not a link
    expect(screen.queryByRole("link", { name: "Party Snacks" })).not.toBeInTheDocument();
    // Active lists are still links
    expect(screen.getByRole("link", { name: "Groceries" })).toBeInTheDocument();
  });

  it("shows 'Use template' button only for owned template lists", async () => {
    const ownedTemplate: ShoppingList = {
      id: "tmpl-own",
      name: "My Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };
    const sharedTemplate: ShoppingList = {
      id: "tmpl-shared",
      name: "Shared Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "other-user-id",
      isTemplate: true,
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [ownedTemplate, sharedTemplate],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("My Template");

    const useTemplateButtons = screen.getAllByRole("button", { name: "Use template" });
    // Only the owned template gets the button
    expect(useTemplateButtons).toHaveLength(1);
    // Shared template has no "Use template" button
    expect(screen.queryByRole("button", { name: "Untemplate" })).toBeInTheDocument(); // owned
  });

  // ---------------------------------------------------------------------------
  // Use-template modal — "Create new list" flow
  // ---------------------------------------------------------------------------

  it("opens Use template modal when 'Use template' is clicked", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-4",
      name: "Christmas List",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [...mockListsData, templateList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Christmas List");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));

    // The modal is open: the tab buttons are present
    expect(screen.getByRole("button", { name: "Create new list" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add to existing list" })).toBeInTheDocument();
    // Name input is visible (modal "new" mode by default)
    expect(screen.getByPlaceholderText("New list name")).toBeInTheDocument();
  });

  it("creates a new list from template and closes the modal on success", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-5",
      name: "Holiday Packing",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };
    const createdList: ShoppingList = {
      id: "new-from-tmpl",
      name: "Holiday Packing (copy)",
      createdAt: "2025-05-01T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: false,
    };

    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => [templateList] } as Response)
      .mockResolvedValueOnce({ ok: true, json: async () => createdList } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Holiday Packing");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));

    // Default mode is "new" — input should be pre-filled with "<name> (copy)"
    const nameInput = screen.getByPlaceholderText("New list name");
    expect(nameInput).toHaveValue("Holiday Packing (copy)");

    fireEvent.click(screen.getByRole("button", { name: "Create list" }));

    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppinglists/tmpl-5/createfrom",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ name: "Holiday Packing (copy)" }),
      })
    );

    // Modal closes and new list appears
    await screen.findByText("Holiday Packing (copy)");
    expect(screen.queryByPlaceholderText("New list name")).not.toBeInTheDocument();
  });

  it("shows error in modal when createfrom API call fails", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-6",
      name: "Error Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => [templateList] } as Response)
      .mockResolvedValueOnce({ ok: false, status: 500, json: async () => ({}) } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Error Template");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));
    fireEvent.click(screen.getByRole("button", { name: "Create list" }));

    expect(
      await screen.findByText("Failed to create list from template.")
    ).toBeInTheDocument();
    // Modal stays open
    expect(screen.getByPlaceholderText("New list name")).toBeInTheDocument();
  });

  it("closes Use template modal when Cancel is clicked", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-cancel",
      name: "Cancel Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [templateList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Cancel Template");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));
    expect(screen.getByPlaceholderText("New list name")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByPlaceholderText("New list name")).not.toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // Use-template modal — "Add to existing list" flow
  // ---------------------------------------------------------------------------

  it("can switch to 'Add to existing list' tab in the Use template modal", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-7",
      name: "Barbecue Supplies",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [...mockListsData, templateList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Barbecue Supplies");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));

    // Switch to "Add to existing list" tab
    fireEvent.click(screen.getByRole("button", { name: "Add to existing list" }));

    // A select dropdown with active lists should appear
    expect(screen.getByRole("combobox")).toBeInTheDocument();
    expect(screen.queryByPlaceholderText("New list name")).not.toBeInTheDocument();
  });

  it("appends template to existing list and closes modal on success", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-8",
      name: "Pantry Restock",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [...mockListsData, templateList],
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ addedCount: 5, mergedCount: 2 }),
      } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Pantry Restock");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));

    // Switch to existing list tab
    fireEvent.click(screen.getByRole("button", { name: "Add to existing list" }));

    // Submit with the pre-selected first active list
    fireEvent.click(screen.getByRole("button", { name: "Add items" }));

    expect(mockAuthFetch).toHaveBeenCalledWith(
      `/api/shoppinglists/tmpl-8/appendto/${mockListsData[0].id}`,
      expect.objectContaining({ method: "POST" })
    );

    // Modal closes after success
    await waitFor(() => {
      expect(screen.queryByRole("button", { name: "Add items" })).not.toBeInTheDocument();
    });
  });

  it("shows error when appendto API call fails", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-9",
      name: "Append Fail Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [...mockListsData, templateList],
      } as Response)
      .mockResolvedValueOnce({ ok: false, status: 500, json: async () => ({}) } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Append Fail Template");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));
    fireEvent.click(screen.getByRole("button", { name: "Add to existing list" }));
    fireEvent.click(screen.getByRole("button", { name: "Add items" }));

    expect(
      await screen.findByText("Failed to apply template to list.")
    ).toBeInTheDocument();
    // Modal remains open
    expect(screen.getByRole("combobox")).toBeInTheDocument();
  });

  it("shows error when appendto API call throws a network error", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-10",
      name: "Network Fail Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => [...mockListsData, templateList],
      } as Response)
      .mockRejectedValueOnce(new Error("Network error"));

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Network Fail Template");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));
    fireEvent.click(screen.getByRole("button", { name: "Add to existing list" }));
    fireEvent.click(screen.getByRole("button", { name: "Add items" }));

    expect(
      await screen.findByText("Failed to connect to the server.")
    ).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // "Add to existing list" tab disabled when no active lists exist
  // ---------------------------------------------------------------------------

  it("disables 'Add to existing list' tab when there are no active lists", async () => {
    const templateList: ShoppingList = {
      id: "tmpl-only",
      name: "Only Template",
      createdAt: "2025-04-03T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [templateList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Only Template");

    fireEvent.click(screen.getByRole("button", { name: "Use template" }));

    const addToExistingTab = screen.getByRole("button", { name: "Add to existing list" });
    expect(addToExistingTab).toBeDisabled();
  });

  // ---------------------------------------------------------------------------
  // Non-owner "(shared)" badge
  // ---------------------------------------------------------------------------

  it("shows '(shared)' badge on lists not owned by the current user", async () => {
    const sharedList: ShoppingList = {
      id: "list-shared",
      name: "Shared With Me",
      createdAt: "2025-04-03T12:00:00Z",
      items: [],
      userId: "other-user-id",
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [sharedList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Shared With Me");

    expect(screen.getByText("(shared)")).toBeInTheDocument();
  });

  it("does not show '(shared)' badge on lists owned by the current user", async () => {
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => mockListsData,
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    expect(screen.queryByText("(shared)")).not.toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // Delete button not shown for non-owner lists
  // ---------------------------------------------------------------------------

  it("does not show Delete button for lists the user does not own", async () => {
    const sharedList: ShoppingList = {
      id: "list-nodel",
      name: "Not My List",
      createdAt: "2025-04-03T12:00:00Z",
      items: [],
      userId: "other-user-id",
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [sharedList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Not My List");

    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // SharePanel
  // ---------------------------------------------------------------------------

  it("shows SharePanel when Share button is clicked for an active list", async () => {
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => mockListsData,
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    // SharePanel should not be visible initially
    expect(screen.queryByTestId("share-panel")).not.toBeInTheDocument();

    // Click the Share button for the first list
    const shareButtons = screen.getAllByRole("button", { name: "Share" });
    fireEvent.click(shareButtons[0]);

    // SharePanel should now be visible with correct endpoint
    const sharePanel = screen.getByTestId("share-panel");
    expect(sharePanel).toBeInTheDocument();
    expect(sharePanel).toHaveAttribute("data-endpoint", `/api/listshares?listId=list-1`);
  });

  it("closes SharePanel when Share button is clicked a second time", async () => {
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => mockListsData,
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    const shareButtons = screen.getAllByRole("button", { name: "Share" });
    fireEvent.click(shareButtons[0]);
    expect(screen.getByTestId("share-panel")).toBeInTheDocument();

    // The button now says "Close"
    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(screen.queryByTestId("share-panel")).not.toBeInTheDocument();
  });

  it("passes isOwner=true to SharePanel for owned lists", async () => {
    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => mockListsData,
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    fireEvent.click(screen.getAllByRole("button", { name: "Share" })[0]);

    const sharePanel = screen.getByTestId("share-panel");
    expect(sharePanel).toHaveAttribute("data-is-owner", "true");
  });

  it("passes isOwner=false to SharePanel for shared (non-owned) lists", async () => {
    const sharedList: ShoppingList = {
      id: "list-share-view",
      name: "View Only List",
      createdAt: "2025-04-03T12:00:00Z",
      items: [],
      userId: "other-user-id",
    };

    mockAuthFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [sharedList],
    } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("View Only List");

    fireEvent.click(screen.getByRole("button", { name: "Share" }));

    const sharePanel = screen.getByTestId("share-panel");
    expect(sharePanel).toHaveAttribute("data-is-owner", "false");
  });

  // ---------------------------------------------------------------------------
  // "Save as template" checkbox in create form
  // ---------------------------------------------------------------------------

  it("can create a new list marked as a template via the checkbox", async () => {
    const templateFromForm: ShoppingList = {
      id: "new-tmpl",
      name: "Batch Cook",
      createdAt: "2025-05-01T10:00:00Z",
      items: [],
      userId: "test-user-id",
      isTemplate: true,
    };

    mockAuthFetch
      .mockResolvedValueOnce({ ok: true, json: async () => mockListsData } as Response)
      .mockResolvedValueOnce({ ok: true, json: async () => templateFromForm } as Response);

    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);
    await screen.findByText("Groceries");

    fireEvent.click(screen.getByRole("button", { name: "Create New List" }));

    const nameInput = screen.getByLabelText("New list name");
    fireEvent.change(nameInput, { target: { value: "Batch Cook" } });

    const templateCheckbox = screen.getByRole("checkbox", { name: /reusable template/i });
    fireEvent.click(templateCheckbox);
    expect(templateCheckbox).toBeChecked();

    fireEvent.click(screen.getByRole("button", { name: "Add Item" }));

    expect(mockAuthFetch).toHaveBeenCalledWith(
      "/api/shoppinglists",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ name: "Batch Cook", isTemplate: true }),
      })
    );

    // New template appears in Templates section
    await screen.findByText("Batch Cook");
    expect(screen.getByText("Templates")).toBeInTheDocument();
  });
});
