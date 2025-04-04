import { render, screen } from "@testing-library/react";
import { describe, it, vi, expect, beforeEach } from "vitest"; // Added beforeEach
// import type { MockedFunction } from 'vitest'; // Not needed with factory mock approach
import { MemoryRouter } from "react-router-dom";
import type { AuthState } from "../store/authStore"; // Import the type

import Layout from "./Layout"; // Component under test

// --- Define variable to hold the mock state for each test ---
let mockStoreState: AuthState;

// --- Mock the authStore module ONCE at the top level ---
vi.mock('../store/authStore', () => ({
  // Factory returns an object where 'default' is the mocked hook
  default: vi.fn(() => mockStoreState), // The mock hook returns our variable state
}));

describe('Layout Component', () => {

  // --- Reset the mock state before EACH test ---
  beforeEach(() => {
    // Default to a logged-out state
    mockStoreState = {
      isAuthenticated: false,
      user: null,
      isLoading: false, // Assuming default is not loading
      // Provide mocks for functions even if not used in Layout, to match type
      checkAuthStatus: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
    };
    // Reset the mock hook's call history etc. if needed (often handled by mockReset or clearAllMocks if you were mocking differently)
    // With this factory pattern, resetting the 'mockStoreState' variable is key.
    // You could also get the mock function instance if needed:
    // const mockedHook = require('../store/authStore').default;
    // mockedHook.mockClear(); // If you needed to check calls TO the hook
  });

  it('renders the header with the Nimblist title', () => {
    // No specific auth state needed, uses default from beforeEach
    render(<MemoryRouter><Layout /></MemoryRouter>);
    const title = screen.getByRole('heading', { level: 1, name: /nimblist/i });
    expect(title).toBeInTheDocument();
  });

  it('renders the Home link', () => {
    // No specific auth state needed
    render(<MemoryRouter><Layout /></MemoryRouter>);
    const homeLink = screen.getByRole('link', { name: /home/i });
    expect(homeLink).toBeInTheDocument();
    expect(homeLink).toHaveAttribute('href', '/');
  });

  it('renders the Login/Register link when not authenticated', () => {
    // Arrange: beforeEach already set state to logged-out/not authenticated
    // No override needed here.

    // Act
    render(<MemoryRouter><Layout /></MemoryRouter>);

    // Assert
    const loginLink = screen.getByRole('link', { name: /login \/ register/i });
    expect(loginLink).toBeInTheDocument();
    // Make sure authenticated links are NOT present
    expect(screen.queryByRole('link', { name: /my lists/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /logout/i })).not.toBeInTheDocument();
  });

  it('renders the My Lists link and Logout button when authenticated', () => {
    // Arrange: Override the default state set in beforeEach for THIS test
    mockStoreState = {
      ...mockStoreState, // Keep function mocks from default state
      isAuthenticated: true,
      user: { userId: 'user123', email: 'test@example.com' }, // Add user data
    };

    // Act
    render(<MemoryRouter><Layout /></MemoryRouter>);

    // Assert
    const myListsLink = screen.getByRole('link', { name: /my lists/i });
    expect(myListsLink).toBeInTheDocument();
    expect(myListsLink).toHaveAttribute('href', '/lists');

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    expect(logoutButton).toBeInTheDocument();

    // Make sure Login link is NOT present
    expect(screen.queryByRole('link', { name: /login \/ register/i })).not.toBeInTheDocument();
  });

  it('renders the user email when authenticated', () => {
    // Arrange: Override the default state
    mockStoreState = {
      ...mockStoreState,
      isAuthenticated: true,
      user: { userId: 'user123', email: 'test@example.com' },
    };

    // Act
    render(<MemoryRouter><Layout /></MemoryRouter>);

    // Assert
    // Ensure the text exists - adjust if your component renders it differently
    const userEmail = screen.getByText('(test@example.com)');
    expect(userEmail).toBeInTheDocument();
  });

  it('renders the footer with the current year', () => {
    // No specific auth state needed
    render(<MemoryRouter><Layout /></MemoryRouter>);
    // Check against current year 2025 based on context
    const currentYear = 2025; // Or new Date().getFullYear() if context wasn't fixed
    const footerText = screen.getByText(new RegExp(`© ${currentYear} Nimblist`, 'i'));
    expect(footerText).toBeInTheDocument();
  });
});