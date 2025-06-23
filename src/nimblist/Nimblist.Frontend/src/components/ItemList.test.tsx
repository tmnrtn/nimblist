import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, vi, expect } from 'vitest';


vi.mock('./ItemList', async () => { // <-- Make the factory function async
  const actual = await vi.importActual('./ItemList'); // <-- Await the promise here
  return {
    ...actual, // Now 'actual' is the resolved module object with all exports
    authenticatedFetch: vi.fn(), // Override authenticatedFetch
  };
});

import ItemList from './ItemList';
import { Item } from '../types';
import authenticatedFetch from './ItemList'; // Import the default exported function to mock

describe('ItemList Component', () => {
  const mockItems: Item[] = [
    {
      id: '1',
      name: 'Item 1',
      isChecked: false,
      quantity: '1',
      addedAt: '2023-01-01T00:00:00Z',
      shoppingListId: 'list-1',
      categoryName: '',
      subCategoryName: ''
    },
    {
      id: '2',
      name: 'Item 2',
      isChecked: false,
      quantity: '2',
      addedAt: '2023-01-02T00:00:00Z',
      shoppingListId: 'list-1',
      categoryName: '',
      subCategoryName: ''
    },
  ];

  it('renders the list of items', () => {
    render(<ItemList initialItems={mockItems} listId="list-1" onDeleteItem={() => { throw new Error('Function not implemented.'); }} onEditItem={() => { throw new Error('Function not implemented.'); }} onDeleteAllChecked={() => { throw new Error('Function not implemented.'); }} />);
    expect(screen.getByText('Item 1')).toBeInTheDocument();
    expect(screen.getByText('Item 2')).toBeInTheDocument();

  });

  it('displays a message when the list is empty', () => {
    render(<ItemList initialItems={[]} listId="test-list-id" onDeleteItem={() => { throw new Error('Function not implemented.'); }} onEditItem={() => { throw new Error('Function not implemented.'); }} onDeleteAllChecked={() => { throw new Error('Function not implemented.'); }} />);
    expect(screen.getByText('This list is empty.')).toBeInTheDocument();
  });

  // it('toggles the checkbox state of an item', async () => {
  //   render(<ItemList initialItems={mockItems} listId="test-list-id" />);
  //   const checkbox = screen.getByLabelText(/Item 1/i);
  //   expect(checkbox).not.toBeChecked();

  //   fireEvent.click(checkbox);
  //   await waitFor(() => expect(checkbox).toBeChecked());
  // });

    
    it.skip('deletes an item from the list', async () => {
    vi.fn(authenticatedFetch).mockResolvedValueOnce('Success');
    render(<ItemList initialItems={mockItems} listId="test-list-id" onDeleteItem={() => { throw new Error('Function not implemented.'); }} onEditItem={() => { throw new Error('Function not implemented.'); }} onDeleteAllChecked={() => { throw new Error('Function not implemented.'); }} />);
    const deleteButton = screen.getByTitle('Delete item "Item 1"');
    vi.spyOn(window, 'confirm').mockReturnValueOnce(true);
    fireEvent.click(deleteButton);
    await waitFor(() => expect(screen.queryByText('Item 1')).not.toBeInTheDocument());
    });

  // it('displays an error message if the delete fails', async () => {
  //   vi.fn(authenticatedFetch).mockRejectedValueOnce(new Error('Delete failed'));
  //   render(<ItemList initialItems={mockItems} listId="test-list-id" onDeleteItem={function (_itemId: string, _itemName: string): void {
  //     throw new Error('Function not implemented.');
  //   } } onEditItem={function (_item: Item, _update: { name: string; quantity: string | null; categoryId: string | null; subCategoryId: string | null; }): void {
  //     throw new Error('Function not implemented.');
  //   } } onDeleteAllChecked={function (): void {
  //     throw new Error('Function not implemented.');
  //   } } />);
  //   const deleteButton = screen.getByTitle('Delete item "Item 1"');

  //   vi.spyOn(window, 'confirm').mockReturnValueOnce(true);

  //   fireEvent.click(deleteButton);

  //   await waitFor(() => expect(screen.getByText(/Failed to delete item/i)).toBeInTheDocument());
  // });
});
