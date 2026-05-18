import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import type { AuthState } from '../store/authStore';
import HomePage from './HomePage';

let mockStoreState: AuthState;
vi.mock('../store/authStore', () => ({
  default: vi.fn(() => mockStoreState),
}));

const renderComponent = () =>
  render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>
  );

describe('HomePage Component', () => {
  beforeEach(() => {
    mockStoreState = {
      isAuthenticated: false,
      isAdmin: false,
  isPaid: false,
      user: null,
      isLoading: false,
      checkAuthStatus: vi.fn(),
      logout: vi.fn(),
    };
  });

  it('renders the hero heading for unauthenticated users', () => {
    renderComponent();
    expect(screen.getByRole('heading', { level: 2, name: /shopping lists and recipes/i })).toBeInTheDocument();
  });

  it('renders a sign-up CTA for unauthenticated users', () => {
    renderComponent();
    expect(screen.getByText(/get started free/i)).toBeInTheDocument();
  });

  it('renders nothing for authenticated users (redirect pending)', () => {
    mockStoreState = { ...mockStoreState, isAuthenticated: true };
    renderComponent();
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
  });
});
