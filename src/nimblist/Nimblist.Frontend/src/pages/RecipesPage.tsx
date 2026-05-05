import React, { useEffect, useState, FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { authenticatedFetch } from '../components/HttpHelper';
import { RecipeSummary } from '../types/index';

interface IngredientRow {
  text: string;
}

const RecipesPage: React.FC = () => {
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [mode, setMode] = useState<'import' | 'manual'>('import');

  // Import form state
  const [importUrl, setImportUrl] = useState('');
  const [isImporting, setIsImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);

  // Manual create form state
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [yields, setYields] = useState('');
  const [totalTimeMinutes, setTotalTimeMinutes] = useState('');
  const [instructions, setInstructions] = useState('');
  const [ingredients, setIngredients] = useState<IngredientRow[]>([{ text: '' }]);
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  useEffect(() => {
    authenticatedFetch('/api/recipes')
      .then(r => r.json())
      .then((data: RecipeSummary[]) => setRecipes(data))
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

      {/* List */}
      {isLoading && <p className="text-gray-500">Loading recipes…</p>}
      {error && <p className="text-red-600">{error}</p>}
      {!isLoading && !error && recipes.length === 0 && (
        <p className="text-gray-500">No recipes yet. Import one or create one above!</p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {recipes.map(recipe => (
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
    </div>
  );
};

export default RecipesPage;
