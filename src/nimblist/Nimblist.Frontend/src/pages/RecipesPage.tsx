import React, { useEffect, useState, FormEvent, useRef } from 'react';
import { Link } from 'react-router-dom';
import { authenticatedFetch } from '../components/HttpHelper';
import { RecipeSummary, ShoppingList } from '../types/index';

interface IngredientRow {
  text: string;
}

const RecipesPage: React.FC = () => {
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [lists, setLists] = useState<ShoppingList[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [mode, setMode] = useState<'import' | 'image' | 'manual'>('import');

  // Import from URL state
  const [importUrl, setImportUrl] = useState('');
  const [isImporting, setIsImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);

  // Import from image state
  const imageInputRef = useRef<HTMLInputElement>(null);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const [isImportingImage, setIsImportingImage] = useState(false);
  const [imageImportError, setImageImportError] = useState<string | null>(null);

  // Manual create form state
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [yields, setYields] = useState('');
  const [totalTimeMinutes, setTotalTimeMinutes] = useState('');
  const [instructions, setInstructions] = useState('');
  const [ingredients, setIngredients] = useState<IngredientRow[]>([{ text: '' }]);
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // Add-to-list inline state
  const [recipeIdToAddToList, setRecipeIdToAddToList] = useState<string | null>(null);
  const [selectedListId, setSelectedListId] = useState('');
  const [isAddingToList, setIsAddingToList] = useState(false);
  const [addToListResult, setAddToListResult] = useState<{ recipeId: string; message: string } | null>(null);

  useEffect(() => {
    Promise.all([
      authenticatedFetch('/api/recipes').then(r => r.json()),
      authenticatedFetch('/api/shoppinglists').then(r => r.json()),
    ])
      .then(([recipesData, listsData]) => {
        setRecipes(recipesData);
        setLists(listsData);
        if (listsData.length > 0) setSelectedListId(listsData[0].id);
      })
      .catch(() => setError('Failed to load recipes.'))
      .finally(() => setIsLoading(false));
  }, []);

  const handleImport = async (e: FormEvent) => {
    e.preventDefault();
    if (!importUrl.trim()) return;
    setIsImporting(true);
    setImportError(null);
    try {
      const response = await authenticatedFetch('/api/recipes/import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url: importUrl.trim() }),
      });
      if (response.ok) {
        const newRecipe = await response.json();
        setRecipes(prev => [
          { id: newRecipe.id, title: newRecipe.title, imageUrl: newRecipe.imageUrl,
            yields: newRecipe.yields, totalTimeMinutes: newRecipe.totalTimeMinutes,
            ingredientCount: newRecipe.ingredients?.length ?? 0, createdAt: newRecipe.createdAt,
            isOwned: true },
          ...prev,
        ]);
        setImportUrl('');
      } else {
        const body = await response.json().catch(() => null);
        setImportError(body?.error ?? `Import failed (${response.status})`);
      }
    } catch {
      setImportError('Network error — could not reach the import service.');
    } finally {
      setIsImporting(false);
    }
  };

  const handleImageSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    setImageFile(file);
    setImageImportError(null);
    if (file) {
      const reader = new FileReader();
      reader.onload = ev => setImagePreview(ev.target?.result as string);
      reader.readAsDataURL(file);
    } else {
      setImagePreview(null);
    }
  };

  const handleImportFromImage = async (e: FormEvent) => {
    e.preventDefault();
    if (!imageFile) return;
    setIsImportingImage(true);
    setImageImportError(null);
    try {
      const formData = new FormData();
      formData.append('image', imageFile);
      // No Content-Type header — browser sets it with the multipart boundary automatically
      const response = await authenticatedFetch('/api/recipes/import-image', {
        method: 'POST',
        body: formData,
      });
      if (response.ok) {
        const newRecipe = await response.json();
        setRecipes(prev => [
          { id: newRecipe.id, title: newRecipe.title, imageUrl: newRecipe.imageUrl,
            yields: newRecipe.yields, totalTimeMinutes: newRecipe.totalTimeMinutes,
            ingredientCount: newRecipe.ingredients?.length ?? 0, createdAt: newRecipe.createdAt,
            isOwned: true },
          ...prev,
        ]);
        setImageFile(null);
        setImagePreview(null);
        if (imageInputRef.current) imageInputRef.current.value = '';
      } else {
        const body = await response.json().catch(() => null);
        setImageImportError(body?.error ?? `Import failed (${response.status})`);
      }
    } catch {
      setImageImportError('Network error — could not reach the import service.');
    } finally {
      setIsImportingImage(false);
    }
  };

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    const validIngredients = ingredients.filter(i => i.text.trim());
    if (!title.trim()) return;
    setIsCreating(true);
    setCreateError(null);
    try {
      const response = await authenticatedFetch('/api/recipes', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title: title.trim(),
          description: description.trim() || null,
          yields: yields.trim() || null,
          totalTimeMinutes: totalTimeMinutes ? parseInt(totalTimeMinutes) : null,
          instructions: instructions.trim() || null,
          ingredients: validIngredients.map((ing, i) => ({
            text: ing.text.trim(),
            parsedName: null,
            parsedQuantity: null,
            sortOrder: i,
          })),
        }),
      });
      if (response.ok) {
        const newRecipe = await response.json();
        setRecipes(prev => [
          { id: newRecipe.id, title: newRecipe.title, imageUrl: newRecipe.imageUrl,
            yields: newRecipe.yields, totalTimeMinutes: newRecipe.totalTimeMinutes,
            ingredientCount: newRecipe.ingredients?.length ?? 0, createdAt: newRecipe.createdAt,
            isOwned: true },
          ...prev,
        ]);
        setTitle(''); setDescription(''); setYields(''); setTotalTimeMinutes('');
        setInstructions(''); setIngredients([{ text: '' }]);
      } else {
        const body = await response.json().catch(() => null);
        setCreateError(body?.title ?? `Failed to create recipe (${response.status})`);
      }
    } catch {
      setCreateError('Network error — could not save the recipe.');
    } finally {
      setIsCreating(false);
    }
  };

  const updateIngredient = (index: number, value: string) => {
    setIngredients(prev => prev.map((ing, i) => i === index ? { text: value } : ing));
  };

  const addIngredientRow = () => setIngredients(prev => [...prev, { text: '' }]);

  const removeIngredientRow = (index: number) => {
    if (ingredients.length === 1) return;
    setIngredients(prev => prev.filter((_, i) => i !== index));
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this recipe?')) return;
    const prev = recipes;
    setRecipes(r => r.filter(x => x.id !== id));
    try {
      await authenticatedFetch(`/api/recipes/${id}`, { method: 'DELETE' });
    } catch {
      setRecipes(prev);
    }
  };

  const handleAddToList = async (recipeId: string) => {
    if (!selectedListId) return;
    setIsAddingToList(true);
    try {
      const response = await authenticatedFetch(
        `/api/recipes/${recipeId}/addtolist/${selectedListId}`,
        { method: 'POST' }
      );
      if (response.ok) {
        const data = await response.json();
        setAddToListResult({
          recipeId,
          message: `Added ${data.addedCount} item${data.addedCount !== 1 ? 's' : ''}`
        });
        setRecipeIdToAddToList(null);
        setTimeout(() => setAddToListResult(null), 3000);
      }
    } catch { /* ignore */ }
    finally { setIsAddingToList(false); }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-2xl font-bold text-gray-800">My Recipes</h2>

      {/* Mode tabs */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex gap-6">
          <button
            onClick={() => setMode('import')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              mode === 'import'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Import from URL
          </button>
          <button
            onClick={() => setMode('image')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              mode === 'image'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Import from Image
          </button>
          <button
            onClick={() => setMode('manual')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              mode === 'manual'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Create Manually
          </button>
        </nav>
      </div>

      {/* Import form */}
      {mode === 'import' && (
        <form onSubmit={handleImport} className="p-4 bg-gray-100 rounded-md border border-gray-200 space-y-3">
          {importError && (
            <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">{importError}</p>
          )}
          <div className="flex gap-2">
            <input
              type="url"
              value={importUrl}
              onChange={e => setImportUrl(e.target.value)}
              placeholder="https://www.example.com/recipe/..."
              required
              disabled={isImporting}
              className="flex-grow px-3 py-2 border border-gray-300 rounded-md shadow-sm text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
            />
            <button
              type="submit"
              disabled={isImporting || !importUrl.trim()}
              aria-busy={isImporting}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-md shadow-sm hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isImporting ? 'Importing…' : 'Import'}
            </button>
          </div>
          <p className="text-xs text-gray-500">
            Supports 500+ recipe sites. Results may vary for unsupported sites.
          </p>
        </form>
      )}

      {/* Import from image form */}
      {mode === 'image' && (
        <form onSubmit={handleImportFromImage} className="p-4 bg-gray-100 rounded-md border border-gray-200 space-y-3">
          {imageImportError && (
            <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">{imageImportError}</p>
          )}
          <div className="flex flex-col items-center gap-3">
            {imagePreview ? (
              <img src={imagePreview} alt="Recipe preview" className="max-h-64 rounded-md border border-gray-300 object-contain" />
            ) : (
              <div
                className="w-full h-40 flex flex-col items-center justify-center border-2 border-dashed border-gray-300 rounded-md cursor-pointer hover:border-indigo-400 transition-colors"
                onClick={() => imageInputRef.current?.click()}
              >
                <span className="text-3xl mb-1">📷</span>
                <span className="text-sm text-gray-500">Tap to take a photo or choose an image</span>
              </div>
            )}
            <input
              ref={imageInputRef}
              type="file"
              accept="image/*"
              capture="environment"
              onChange={handleImageSelect}
              disabled={isImportingImage}
              className="sr-only"
            />
            <div className="flex gap-2 w-full">
              <button
                type="button"
                onClick={() => imageInputRef.current?.click()}
                disabled={isImportingImage}
                className="flex-1 px-4 py-2 border border-gray-300 text-sm text-gray-700 rounded-md hover:bg-gray-50 disabled:opacity-50 transition-colors"
              >
                {imageFile ? 'Change image' : 'Choose image'}
              </button>
              <button
                type="submit"
                disabled={isImportingImage || !imageFile}
                aria-busy={isImportingImage}
                className="flex-1 px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-md shadow-sm hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isImportingImage ? 'Importing…' : 'Import Recipe'}
              </button>
            </div>
          </div>
          <p className="text-xs text-gray-500">
            Take a photo of a recipe card, book page, or printed recipe. Requires a vision-capable LLM to be configured.
          </p>
        </form>
      )}

      {/* Manual create form */}
      {mode === 'manual' && (
        <form onSubmit={handleCreate} className="p-4 bg-gray-100 rounded-md border border-gray-200 space-y-4">
          {createError && (
            <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">{createError}</p>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="sm:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">Title *</label>
              <input
                type="text"
                value={title}
                onChange={e => setTitle(e.target.value)}
                required
                disabled={isCreating}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Yields</label>
              <input
                type="text"
                value={yields}
                onChange={e => setYields(e.target.value)}
                placeholder="e.g. 4 servings"
                disabled={isCreating}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Total time (minutes)</label>
              <input
                type="number"
                min="1"
                value={totalTimeMinutes}
                onChange={e => setTotalTimeMinutes(e.target.value)}
                disabled={isCreating}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">Description</label>
              <textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                rows={2}
                disabled={isCreating}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
              />
            </div>
          </div>

          {/* Ingredients */}
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-2">Ingredients</label>
            <div className="space-y-2">
              {ingredients.map((ing, i) => (
                <div key={i} className="flex gap-2">
                  <input
                    type="text"
                    value={ing.text}
                    onChange={e => updateIngredient(i, e.target.value)}
                    placeholder={`e.g. 2 cups flour`}
                    disabled={isCreating}
                    className="flex-grow px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
                  />
                  <button
                    type="button"
                    onClick={() => removeIngredientRow(i)}
                    disabled={ingredients.length === 1 || isCreating}
                    className="px-2 text-gray-400 hover:text-red-500 disabled:opacity-30 transition-colors"
                    aria-label="Remove ingredient"
                  >
                    ✕
                  </button>
                </div>
              ))}
            </div>
            <button
              type="button"
              onClick={addIngredientRow}
              disabled={isCreating}
              className="mt-2 text-sm text-indigo-600 hover:text-indigo-800 disabled:opacity-50"
            >
              + Add ingredient
            </button>
          </div>

          {/* Instructions */}
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Instructions</label>
            <textarea
              value={instructions}
              onChange={e => setInstructions(e.target.value)}
              rows={5}
              placeholder="Enter each step on a new line"
              disabled={isCreating}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
            />
          </div>

          <button
            type="submit"
            disabled={isCreating || !title.trim()}
            aria-busy={isCreating}
            className="px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-md shadow-sm hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {isCreating ? 'Saving…' : 'Save Recipe'}
          </button>
        </form>
      )}

      {/* Search */}
      {!isLoading && !error && recipes.length > 0 && (
        <input
          type="search"
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          placeholder="Search recipes…"
          className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500"
        />
      )}

      {/* List */}
      {isLoading && <p className="text-gray-500">Loading recipes…</p>}
      {error && <p className="text-red-600">{error}</p>}
      {!isLoading && !error && recipes.length === 0 && (
        <p className="text-gray-500">No recipes yet. Import one or create one above!</p>
      )}

      {(() => {
        const q = searchQuery.trim().toLowerCase();
        const visible = q
          ? recipes.filter(r =>
              r.title.toLowerCase().includes(q) ||
              (r.yields ?? '').toLowerCase().includes(q)
            )
          : recipes;
        return (
          <>
            {!isLoading && !error && q && visible.length === 0 && (
              <p className="text-gray-500">No recipes match "{searchQuery}".</p>
            )}
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {visible.map(recipe => (
                <div key={recipe.id} className="bg-white rounded-lg shadow border border-gray-200 overflow-hidden flex flex-col">
                  {recipe.imageUrl && (
                    <img
                      src={recipe.imageUrl}
                      alt={recipe.title}
                      className="w-full h-40 object-cover"
                      onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
                    />
                  )}
                  <div className="p-4 flex flex-col flex-grow">
                    <h3 className="font-semibold text-gray-800 mb-1 line-clamp-2">{recipe.title}</h3>
                    <div className="text-xs text-gray-500 space-x-3 mb-3">
                      {recipe.yields && <span>{recipe.yields}</span>}
                      {recipe.totalTimeMinutes != null && <span>{recipe.totalTimeMinutes} min</span>}
                      <span>{recipe.ingredientCount} ingredients</span>
                    </div>

                    {/* Add to list inline UI */}
                    <div className="mb-4 min-h-[32px]">
                      {addToListResult?.recipeId === recipe.id ? (
                        <p className="text-green-600 text-xs font-medium py-1">{addToListResult.message}</p>
                      ) : recipeIdToAddToList === recipe.id ? (
                        <div className="flex gap-1 animate-in fade-in duration-200">
                          <select
                            value={selectedListId}
                            onChange={e => setSelectedListId(e.target.value)}
                            className="flex-1 text-xs px-2 py-1 border border-gray-300 rounded shadow-sm focus:outline-none focus:ring-1 focus:ring-indigo-500"
                            disabled={isAddingToList}
                            autoFocus
                          >
                            {lists.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
                          </select>
                          <button
                            onClick={() => handleAddToList(recipe.id)}
                            disabled={isAddingToList}
                            className="text-xs px-2 py-1 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50 font-medium"
                          >
                            {isAddingToList ? '…' : 'Add'}
                          </button>
                          <button
                            onClick={() => setRecipeIdToAddToList(null)}
                            disabled={isAddingToList}
                            className="text-xs px-1.5 py-1 text-gray-400 hover:text-gray-600"
                          >
                            ✕
                          </button>
                        </div>
                      ) : (
                        lists.length > 0 && (
                          <button
                            onClick={() => setRecipeIdToAddToList(recipe.id)}
                            className="text-xs font-medium text-indigo-600 hover:text-indigo-800 transition-colors"
                          >
                            + Add ingredients to list
                          </button>
                        )
                      )}
                    </div>

                    <div className="mt-auto flex gap-2">
                      <Link
                        to={`/recipes/${recipe.id}`}
                        className="flex-1 text-center px-3 py-1.5 bg-indigo-600 text-white text-sm font-medium rounded hover:bg-indigo-700 transition-colors"
                      >
                        View
                      </Link>
                      {recipe.isOwned && (
                        <button
                          onClick={() => handleDelete(recipe.id)}
                          className="px-3 py-1.5 text-sm text-red-600 border border-red-300 rounded hover:bg-red-50 transition-colors"
                        >
                          Delete
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </>
        );
      })()}
    </div>
  );
};

export default RecipesPage;

