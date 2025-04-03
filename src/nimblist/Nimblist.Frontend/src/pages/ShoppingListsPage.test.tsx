// src/pages/ShoppingListsPage.test.tsx

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom'; // Needed because component uses <Link>
import type { MockedFunction } from 'vitest';

// Import types and component
import type { AuthState } from '../store/authStore';
import useAuthStore from '../store/authStore';
import type { ShoppingList } from '../types';
import { authenticatedFetch } from '../components/HttpHelper'; // Function to mock
import ShoppingListsPage from './ShoppingListsPage'; // Component under test

// --- Mock Dependencies ---

// 1. Mock authStore module
let mockStoreState: AuthState;
vi.mock('../store/authStore', () => ({
  default: vi.fn(() => mockStoreState),
}));

// 2. Mock HttpHelper module (where authenticatedFetch resides)
vi.mock('../components/HttpHelper');

// --- Test Suite ---
describe('ShoppingListsPage Component', () => {
  // Cast the mocked fetch function for type safety
  const mockAuthFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

  // Define mock list data for successful responses
  const mockListsData: ShoppingList[] = [
    {
        id: 'list-1', name: 'Groceries', createdAt: '2025-04-03T10:00:00Z', items: [],
        userId: 'test-user-id'
    },
    {
        id: 'list-2', name: 'Hardware Store', createdAt: '2025-04-03T11:00:00Z', items: [],
        userId: 'test-user-id'
    },
  ];

  beforeEach(() => {
    // Reset all mocks (clears calls, implementations, etc.) before each test
    vi.clearAllMocks();

    // Set default mock auth state: Authenticated, not loading
    mockStoreState = {
      isAuthenticated: true,
      user: { userId: 'test-user-id', email: 'test@example.com' },
      isLoading: false,
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };

    // Explicitly reset the fetch mock (optional if clearAllMocks is used, but good practice)
    mockAuthFetch.mockReset();
  });

  it('should display login prompt error if user is not authenticated', () => {
    // Arrange: Override auth state for this test
    mockStoreState = { ...mockStoreState, isAuthenticated: false, user: null };

    // Act: Render the component
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Check for the specific error message set when not authenticated
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Please log in to view your shopping lists.'
    );
    // Assert: Fetch should NOT have been called
    expect(mockAuthFetch).not.toHaveBeenCalled();
  });

  it('should display loading indicator while fetching data', () => {
    // Arrange: Make the fetch promise never resolve to keep it loading
    mockAuthFetch.mockImplementation(() => new Promise(() => {}));

    // Act
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Check for loading text
    expect(screen.getByText('Loading lists...')).toBeInTheDocument();
    // Assert: Fetch *should* have been called (since user is authenticated by default beforeEach)
    expect(mockAuthFetch).toHaveBeenCalledTimes(1);
    expect(mockAuthFetch).toHaveBeenCalledWith('/api/shoppinglists', expect.any(Object));
  });

  it('should fetch and display shopping lists successfully', async () => {
    // Arrange: Mock a successful API response
    const mockSuccessResponse = {
      ok: true,
      status: 200,
      json: async () => mockListsData, // Return the mock data
    } as Response;
    mockAuthFetch.mockResolvedValue(mockSuccessResponse);

    // Act
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Wait for loading to finish and lists to appear
    // Use findBy* which includes waitFor
    expect(await screen.findByText(mockListsData[0].name)).toBeInTheDocument();
    expect(screen.getByText(mockListsData[1].name)).toBeInTheDocument();

    // Check that loading/error states are not present
    expect(screen.queryByText('Loading lists...')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    // Check if links are rendered correctly
    const link1 = screen.getByRole('link', { name: mockListsData[0].name });
    expect(link1).toHaveAttribute('href', `/lists/${mockListsData[0].id}`);
    const link2 = screen.getByRole('link', { name: mockListsData[1].name });
    expect(link2).toHaveAttribute('href', `/lists/${mockListsData[1].id}`);

    // Verify fetch call
    expect(mockAuthFetch).toHaveBeenCalledTimes(1);
    expect(mockAuthFetch).toHaveBeenCalledWith('/api/shoppinglists', expect.objectContaining({ method: 'GET' }));
  });

  it('should display "no lists yet" message when fetch is successful but returns empty array', async () => {
    // Arrange: Mock successful API response with empty data
    const mockEmptyResponse = {
      ok: true,
      status: 200,
      json: async () => [], // Empty array
    } as Response;
    mockAuthFetch.mockResolvedValue(mockEmptyResponse);

    // Act
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Check for the empty state message
    expect(await screen.findByText("You haven't created any lists yet!")).toBeInTheDocument();
    // Check that loading/error states are not present
    expect(screen.queryByText('Loading lists...')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('should display specific error message on API 401 Unauthorized', async () => {
    // Arrange: Mock 401 response
    const mockUnauthorizedResponse = {
      ok: false,
      status: 401,
      text: async () => 'Unauthorized', // Mock methods component might use
      json: async () => ({ message: 'Unauthorized access' })
    } as Response;
    mockAuthFetch.mockResolvedValue(mockUnauthorizedResponse);

    // Act
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Check for the specific 401 error message
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Your session may have expired. Please log out and log back in.'
    );
  });

  it('should display generic error message on other API errors (e.g., 500)', async () => {
    // Arrange: Mock 500 response
    const mockServerErrResponse = {
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
      text: async () => 'Server exploded',
      json: async () => ({})
    } as Response;
    mockAuthFetch.mockResolvedValue(mockServerErrResponse);

    // Act
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Check for the generic error message including status code
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Failed to load lists. Server responded with 500.'
    );
  });

  it('should display network error message if fetch itself rejects', async () => {
    // Arrange: Mock fetch rejection
    const networkError = new Error("Failed to fetch");
    mockAuthFetch.mockRejectedValue(networkError);

    // Act
    render(<MemoryRouter><ShoppingListsPage /></MemoryRouter>);

    // Assert: Check for the network error message handled by the catch block
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to connect to the server. Please check your network connection.'
    );
  });
});