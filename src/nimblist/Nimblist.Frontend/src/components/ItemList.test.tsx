import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, vi, expect, beforeEach } from 'vitest';
import type { MockedFunction } from 'vitest';
import { authenticatedFetch } from '../components/HttpHelper';
import ItemList from './ItemList';
import type { Item } from '../types';

vi.mock('../components/HttpHelper');

describe('ItemList Component', () => {
  const mockAuthFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

  const mockItems: Item[] = [
    {
      id: '1',
      name: 'Item 1',
      isChecked: false,
      quantity: '1',
      addedAt: '2023-01-01T00:00:00Z',
      shoppingListId: 'list-1',
      categoryName: 'Produce',
      subCategoryName: 'Fruit',
      categoryId: 'cat-1',
      subCategoryId: 'sub-1',
    },
    {
      id: '2',
      name: 'Item 2',
      isChecked: false,
      quantity: '2',
      addedAt: '2023-01-02T00:00:00Z',
      shoppingListId: 'list-1',
      categoryName: '',
      subCategoryName: '',
    },
  ];

  const mockOnDeleteItem = vi.fn();
  const mockOnEditItem = vi.fn();
  const mockOnDeleteAllChecked = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Return empty arrays for the categories/subcategories fetches on mount
    mockAuthFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => [],
    } as Response);
  });

  const renderComponent = (items: Item[] = mockItems) =>
    render(
      <ItemList
        initialItems={items}
        listId="list-1"
        onDeleteItem={mockOnDeleteItem}
        onEditItem={mockOnEditItem}
        onDeleteAllChecked={mockOnDeleteAllChecked}
      />
    );

  it('renders the list of items', () => {
    renderComponent();
    expect(screen.getByText('Item 1')).toBeInTheDocument();
    expect(screen.getByText('Item 2')).toBeInTheDocument();
  });

  it('displays a message when the list is empty', () => {
    renderComponent([]);
    expect(screen.getByText('Your list is empty')).toBeInTheDocument();
  });

  it('calls onDeleteItem with the item id and name when delete is confirmed', () => {
    vi.spyOn(window, 'confirm').mockReturnValueOnce(true);
    renderComponent();
    fireEvent.click(screen.getByTitle('Delete item "Item 1"'));
    expect(mockOnDeleteItem).toHaveBeenCalledWith('1', 'Item 1');
  });

  it('does not call onDeleteItem when the confirm dialog is cancelled', () => {
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false);
    renderComponent();
    fireEvent.click(screen.getByTitle('Delete item "Item 1"'));
    expect(mockOnDeleteItem).not.toHaveBeenCalled();
  });

  it('shows the edit form with pre-filled values when Edit is clicked', () => {
    renderComponent();
    fireEvent.click(screen.getByTitle('Edit item "Item 1"'));
    expect(screen.getByDisplayValue('Item 1')).toBeInTheDocument();
    expect(screen.getByDisplayValue('1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
  });

  it('hides the edit form and restores the row when Cancel is clicked', () => {
    renderComponent();
    fireEvent.click(screen.getByTitle('Edit item "Item 1"'));
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument();
    expect(screen.getByText('Item 1')).toBeInTheDocument();
  });

  it('calls onEditItem with updated values when the edit form is submitted', () => {
    renderComponent();
    fireEvent.click(screen.getByTitle('Edit item "Item 1"'));

    const nameInput = screen.getByDisplayValue('Item 1');
    fireEvent.change(nameInput, { target: { value: 'Updated Item' } });

    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(mockOnEditItem).toHaveBeenCalledWith(
      expect.objectContaining({ id: '1' }),
      expect.objectContaining({ name: 'Updated Item' })
    );
  });

  it('shows a validation error and does not call onEditItem when name is cleared', () => {
    renderComponent();
    fireEvent.click(screen.getByTitle('Edit item "Item 1"'));

    const nameInput = screen.getByDisplayValue('Item 1');
    fireEvent.change(nameInput, { target: { value: '' } });
    // Submit the form directly to bypass jsdom's native `required` constraint validation
    fireEvent.submit(nameInput.closest('form')!);

    expect(screen.getByText('Item name is required.')).toBeInTheDocument();
    expect(mockOnEditItem).not.toHaveBeenCalled();
  });
});
