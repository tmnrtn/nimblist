import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import HomePage from './HomePage';

describe('HomePage Component', () => {
  it('renders the heading', () => {
    render(<HomePage />);
    const heading = screen.getByRole('heading', { level: 2, name: /home page/i });
    expect(heading).toBeInTheDocument();
  });

  it('renders the welcome message', () => {
    render(<HomePage />);
    const message = screen.getByText(/welcome to nimblist!/i);
    expect(message).toBeInTheDocument();
  });
});