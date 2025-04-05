import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import LoginPrompt from './LoginPrompt'; // Adjust path if necessary

// --- Test Constants ---
// This should match the value defined in your Vitest config's `define` section
const MOCK_API_BASE_URL = 'https://localhost:64213';
const expectedLoginPageUrl = `${MOCK_API_BASE_URL}/Identity/Account/Login`;

// --- Mocking window.location ---
// Store the original implementation
const originalLocation = window.location;

beforeEach(() => {
  // Before each test, make window.location writable and assign a base mock object
  // This prevents tests from interfering with each other.
  Object.defineProperty(window, 'location', {
    writable: true,
    configurable: true, // Allow redefining properties like pathname/search
    value: {
        // Provide default implementations or initial values if needed
        // but we'll usually override pathname/search per test
        ...originalLocation, // Spread original properties
        assign: vi.fn(), // Mock methods if ever needed
        replace: vi.fn(),
        reload: vi.fn(),
    },
  });
});

afterEach(() => {
  // Restore the original window.location object after each test
  Object.defineProperty(window, 'location', {
    configurable: true, // Keep configurable for next beforeEach
    // Restore original writability (usually false for properties)
    // but restoring the original object handles this implicitly
    value: originalLocation,
  });
  // Reset any Vitest mocks if used elsewhere
  vi.restoreAllMocks();
});

// Helper to set a specific pathname and search for a test
const setTestWindowLocation = (pathname: string, search: string = '') => {
   Object.defineProperty(window.location, 'pathname', {
       writable: true,
       value: pathname,
   });
    Object.defineProperty(window.location, 'search', {
       writable: true,
       value: search,
   });
};

// --- Test Suite ---
describe('LoginPrompt Component', () => {

  it('renders the "Login / Register" link correctly', () => {
    // Arrange: Set a dummy location
    setTestWindowLocation('/some/path');

    // Act
    render(<LoginPrompt />);

    // Assert
    const loginLink = screen.getByRole('link', { name: /login \/ register/i });
    expect(loginLink).toBeInTheDocument();
  });

  it('generates the correct href with returnUrl using only pathname', () => {
    // Arrange
    const testPath = '/products/electronics';
    setTestWindowLocation(testPath); // Set specific location for this test

    // Act
    render(<LoginPrompt />);

    // Assert
    const loginLink = screen.getByRole('link', { name: /login \/ register/i });
    const expectedReturnUrlValue = encodeURIComponent(testPath);
    const expectedHref = `${expectedLoginPageUrl}?returnUrl=${expectedReturnUrlValue}`;
    expect(loginLink).toHaveAttribute('href', expectedHref);
  });

  it('generates the correct href with returnUrl including pathname and search', () => {
    // Arrange
    const testPath = '/user/settings';
    const testSearch = '?section=profile&theme=dark';
    setTestWindowLocation(testPath, testSearch); // Set location with search params

    // Act
    render(<LoginPrompt />);

    // Assert
    const loginLink = screen.getByRole('link', { name: /login \/ register/i });
    const currentPathAndQuery = testPath + testSearch;
    const expectedReturnUrlValue = encodeURIComponent(currentPathAndQuery);
    const expectedHref = `${expectedLoginPageUrl}?returnUrl=${expectedReturnUrlValue}`;
    expect(loginLink).toHaveAttribute('href', expectedHref);
  });

  it.skip('correctly encodes special characters in the returnUrl', () => {
    // Arrange
    const testPath = '/path with spaces/';
    // Characters like space, &, =, %, non-ascii should be encoded
    const testSearch = '?query=a%b c&param=value=d&lang=中文';
    setTestWindowLocation(testPath, testSearch);

    // Act
    render(<LoginPrompt />);

    // Assert
    const loginLink = screen.getByRole('link', { name: /login \/ register/i });
    const currentPathAndQuery = testPath + testSearch;
    const expectedReturnUrlValue = encodeURIComponent(currentPathAndQuery);
    const expectedHref = `${expectedLoginPageUrl}?returnUrl=${expectedReturnUrlValue}`;

    // Check the final href attribute
    expect(loginLink).toHaveAttribute('href', expectedHref);

    // Optional: Decode the parameter to double-check the source was correct
    const generatedUrl = new URL(loginLink.getAttribute('href')!, MOCK_API_BASE_URL);
    const returnUrlParam = generatedUrl.searchParams.get('returnUrl');
    expect(decodeURIComponent(returnUrlParam!)).toBe(currentPathAndQuery);
  });

   it('handles root path correctly', () => {
    // Arrange
    const testPath = '/';
    setTestWindowLocation(testPath);

    // Act
    render(<LoginPrompt />);

    // Assert
    const loginLink = screen.getByRole('link', { name: /login \/ register/i });
    const expectedReturnUrlValue = encodeURIComponent(testPath);
    const expectedHref = `${expectedLoginPageUrl}?returnUrl=${expectedReturnUrlValue}`;
    expect(loginLink).toHaveAttribute('href', expectedHref);
  });
});