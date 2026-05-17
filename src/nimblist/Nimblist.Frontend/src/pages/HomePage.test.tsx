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

  it('renders the heading for unauthenticated users', () => {
    renderComponent();
    expect(screen.getByRole('heading', { level: 2, name: /home page/i })).toBeInTheDocument();
  });

  it('renders the welcome message for unauthenticated users', () => {
    renderComponent();
    expect(screen.getByText(/welcome to nimblist!/i)).toBeInTheDocument();
  });

  it('renders nothing for authenticated users (redirect pending)', () => {
    mockStoreState = { ...mockStoreState, isAuthenticated: true };
    renderComponent();
    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
  });
});
