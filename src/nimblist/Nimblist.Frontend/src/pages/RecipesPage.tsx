import React, { useEffect, useState, FormEvent, useRef } from 'react';
import { Link } from 'react-router-dom';
import { usePageTitle } from '../hooks/usePageTitle';
import { authenticatedFetch } from '../components/HttpHelper';
import { RecipeSummary, ShoppingList, Tag } from '../types/index';
import useAuthStore from '../store/authStore';

interface IngredientRow {
  text: string;
}

const TAG_COLORS = [
  { name: 'red',    bg: 'bg-red-100',    text: 'text-red-700',    dot: 'bg-red-500'    },
  { name: 'orange', bg: 'bg-orange-100', text: 'text-orange-700', dot: 'bg-orange-500' },
  { name: 'yellow', bg: 'bg-yellow-100', text: 'text-yellow-700', dot: 'bg-yellow-500' },
  { name: 'green',  bg: 'bg-green-100',  text: 'text-green-700',  dot: 'bg-green-500'  },
  { name: 'teal',   bg: 'bg-teal-100',   text: 'text-teal-700',   dot: 'bg-teal-500'   },
  { name: 'blue',   bg: 'bg-blue-100',   text: 'text-blue-700',   dot: 'bg-blue-500'   },
  { name: 'indigo', bg: 'bg-indigo-100', text: 'text-indigo-700', dot: 'bg-indigo-500' },
  { name: 'purple', bg: 'bg-purple-100', text: 'text-purple-700', dot: 'bg-purple-500' },
  { name: 'pink',   bg: 'bg-pink-100',   text: 'text-pink-700',   dot: 'bg-pink-500'   },
  { name: 'gray',   bg: 'bg-gray-100',   text: 'text-gray-600',   dot: 'bg-gray-400'   },
] as const;

type ColorName = typeof TAG_COLORS[number]['name'];

function getTagColor(color: string | null) {
  return TAG_COLORS.find(c => c.name === color) ?? TAG_COLORS[TAG_COLORS.length - 1];
}

function TagChip({ tag, onRemove }: { tag: Tag; onRemove?: () => void }) {
  const c = getTagColor(tag.color);
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${c.bg} ${c.text}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${c.dot}`} />
      {tag.name}
      {onRemove && (
        <button onClick={onRemove} className="ml-0.5 hover:opacity-70" aria-label={`Remove tag ${tag.name}`}>
          ✕
        </button>
      )}
    </span>
  );
}

const FREE_RECIPE_LIMIT = 25;

function UpgradePrompt({ message }: { message: string }) {
  return (
    <div className="p-4 bg-indigo-50 border border-indigo-200 rounded-md text-center space-y-2">
      <p className="text-sm text-indigo-800">{message}</p>
      <Link
        to="/billing"
        className="inline-block px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded hover:bg-indigo-700 transition-colors"
      >
        Upgrade to Premium — £1.99/month
      </Link>
      <p className="text-xs text-indigo-500">7-day free trial, cancel anytime</p>
    </div>
  );
}

const RecipesPage: React.FC = () => {
  usePageTitle('Recipes');
  const { isPaid } = useAuthStore();
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [lists, setLists] = useState<ShoppingList[]>([]);
  const [allTags, setAllTags] = useState<Tag[]>([]);
  const [filterTagIds, setFilterTagIds] = useState<Set<string>>(new Set());
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [sortOrder, setSortOrder] = useState<'date' | 'alpha'>('date');
  const [mode, setMode] = useState<'import' | 'image' | 'manual'>('import');

  // Share all panel state
  const [showShareAllPanel, setShowShareAllPanel] = useState(false);
  const [shareAllFamilies, setShareAllFamilies] = useState<{ id: string; name: string }[]>([]);
  const [shareAllFamilyId, setShareAllFamilyId] = useState('');
  const [isShareAllLoading, setIsShareAllLoading] = useState(false);
  const [isShareAllSubmitting, setIsShareAllSubmitting] = useState(false);
  const [shareAllResult, setShareAllResult] = useState<{ sharedCount: number; skippedCount: number; familyName: string } | null>(null);
  const [shareAllError, setShareAllError] = useState<string | null>(null);

  // Tag management panel state
  const [showTagPanel, setShowTagPanel] = useState(false);
  const [tagName, setTagName] = useState('');
  const [tagColor, setTagColor] = useState<ColorName>('blue');
  const [editingTagId, setEditingTagId] = useState<string | null>(null);
  const [isSavingTag, setIsSavingTag] = useState(false);
  const [tagError, setTagError] = useState<string | null>(null);

  // Import from URL state
  const [importUrl, setImportUrl] = useState('');
  const [isImporting, setIsImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [importedRecipeId, setImportedRecipeId] = useState<string | null>(null);

  // Import from image state
  const imageInputRef = useRef<HTMLInputElement>(null);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const [isImportingImage, setIsImportingImage] = useState(false);
  const [imageImportError, setImageImportError] = useState<string | null>(null);
  const [importedImageRecipeId, setImportedImageRecipeId] = useState<string | null>(null);

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

  const activeLists = lists.filter(l => !l.isTemplate);

  useEffect(() => {
    Promise.all([
      authenticatedFetch('/api/recipes').then(r => r.json()),
      authenticatedFetch('/api/shoppinglists').then(r => r.json()),
      authenticatedFetch('/api/tags').then(r => r.json()),
    ])
      .then(([recipesData, listsData, tagsData]) => {
        setRecipes(Array.isArray(recipesData) ? recipesData : []);
        setLists(Array.isArray(listsData) ? listsData : []);
        setAllTags(Array.isArray(tagsData) ? tagsData : []);
        const active = (Array.isArray(listsData) ? listsData as ShoppingList[] : []).filter(l => !l.isTemplate);
        if (active.length > 0) setSelectedListId(active[0].id);
      })
      .catch(() => setError('Failed to load recipes.'))
      .finally(() => setIsLoading(false));
  }, []);

  // ── Tag management ──────────────────────────────────────────────────────────

  const resetTagForm = () => {
    setTagName('');
    setTagColor('blue');
    setEditingTagId(null);
    setTagError(null);
  };

  const startEditTag = (tag: Tag) => {
    setEditingTagId(tag.id);
    setTagName(tag.name);
    setTagColor((tag.color as ColorName) ?? 'blue');
    setTagError(null);
  };

  const handleSaveTag = async (e: FormEvent) => {
    e.preventDefault();
    if (!tagName.trim()) return;
    setIsSavingTag(true);
    setTagError(null);
    try {
      const url = editingTagId ? `/api/tags/${editingTagId}` : '/api/tags';
      const method = editingTagId ? 'PUT' : 'POST';
      const response = await authenticatedFetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: tagName.trim(), color: tagColor }),
      });
      if (response.ok) {
        const saved: Tag = await response.json();
        if (editingTagId) {
          setAllTags(prev => prev.map(t => t.id === editingTagId ? saved : t));
          // Update tags on recipes
          setRecipes(prev => prev.map(r => ({
            ...r,
            tags: r.tags.map(t => t.id === editingTagId ? saved : t),
          })));
        } else {
          setAllTags(prev => [...prev, saved]);
        }
        resetTagForm();
      } else {
        const body = await response.json().catch(() => null);
        setTagError(body?.error ?? `Failed to save tag (${response.status})`);
      }
    } catch {
      setTagError('Network error — could not save tag.');
    } finally {
      setIsSavingTag(false);
    }
  };

  const handleDeleteTag = async (id: string) => {
    if (!confirm('Delete this tag? It will be removed from all recipes.')) return;
    try {
      await authenticatedFetch(`/api/tags/${id}`, { method: 'DELETE' });
      setAllTags(prev => prev.filter(t => t.id !== id));
      setRecipes(prev => prev.map(r => ({ ...r, tags: r.tags.filter(t => t.id !== id) })));
      setFilterTagIds(prev => { const next = new Set(prev); next.delete(id); return next; });
      if (editingTagId === id) resetTagForm();
    } catch { /* ignore */ }
  };

  const toggleFilterTag = (tagId: string) => {
    setFilterTagIds(prev => {
      const next = new Set(prev);
      if (next.has(tagId)) next.delete(tagId); else next.add(tagId);
      return next;
    });
  };

  // ── Share all handlers ──────────────────────────────────────────────────────

  const handleOpenShareAll = async () => {
    setShowShareAllPanel(p => {
      if (p) return false;
      return true;
    });
    if (!showShareAllPanel && shareAllFamilies.length === 0) {
      setIsShareAllLoading(true);
      try {
        const r = await authenticatedFetch('/api/families');
        const data = r.ok ? await r.json() : [];
        setShareAllFamilies(Array.isArray(data) ? data : []);
        if (Array.isArray(data) && data.length > 0) setShareAllFamilyId(data[0].id);
      } catch { /* ignore */ }
      finally { setIsShareAllLoading(false); }
    }
  };

  const handleShareAll = async (e: FormEvent) => {
    e.preventDefault();
    if (!shareAllFamilyId) return;
    setIsShareAllSubmitting(true);
    setShareAllError(null);
    setShareAllResult(null);
    try {
      const response = await authenticatedFetch('/api/recipeshares/share-all', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ familyIdToShareWith: shareAllFamilyId }),
      });
      if (response.ok) {
        const data = await response.json();
        const family = shareAllFamilies.find(f => f.id === shareAllFamilyId);
        setShareAllResult({ sharedCount: data.sharedCount, skippedCount: data.skippedCount, familyName: family?.name ?? 'family' });
      } else {
        const body = await response.json().catch(() => null);
        setShareAllError(body?.message ?? `Failed to share (${response.status})`);
      }
    } catch {
      setShareAllError('Network error — could not share recipes.');
    } finally {
      setIsShareAllSubmitting(false);
    }
  };

  // ── Import handlers ─────────────────────────────────────────────────────────

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
            isOwned: true, tags: [] },
          ...prev,
        ]);
        setImportUrl('');
        setImportedRecipeId(newRecipe.id);
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
            isOwned: true, tags: [] },
          ...prev,
        ]);
        setImageFile(null);
        setImagePreview(null);
        if (imageInputRef.current) imageInputRef.current.value = '';
        setImportedImageRecipeId(newRecipe.id);
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
            isOwned: true, tags: [] },
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

  // ── Derived filtered list ───────────────────────────────────────────────────

  const q = searchQuery.trim().toLowerCase();
  const visibleRecipes = recipes
    .filter(r => filterTagIds.size === 0 || r.tags.some(t => filterTagIds.has(t.id)))
    .filter(r => !q || r.title.toLowerCase().includes(q) || (r.yields ?? '').toLowerCase().includes(q))
    .sort(sortOrder === 'alpha'
      ? (a, b) => a.title.localeCompare(b.title)
      : (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-gray-800">My Recipes</h2>
        {!isPaid && (
          <span className={`text-sm font-medium px-3 py-1 rounded-full ${recipes.length >= FREE_RECIPE_LIMIT ? 'bg-red-100 text-red-700' : 'bg-gray-100 text-gray-600'}`}>
            {recipes.length}/{FREE_RECIPE_LIMIT} recipes (free tier)
          </span>
        )}
      </div>

      {!isPaid && recipes.length >= FREE_RECIPE_LIMIT && (
        <UpgradePrompt message="You've reached the 25-recipe limit on the free plan. Upgrade to save unlimited recipes." />
      )}

      {/* Mode tabs */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex gap-6">
          {(['import', 'image', 'manual'] as const).map(m => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                mode === m
                  ? 'border-indigo-600 text-indigo-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {m === 'import' ? 'Import from URL' : m === 'image' ? 'Import from Image' : 'Create Manually'}
            </button>
          ))}
        </nav>
      </div>

      {/* Import from URL */}
      {mode === 'import' && !isPaid && (
        <UpgradePrompt message="Recipe import from URL is a Premium feature." />
      )}
      {mode === 'import' && isPaid && (
        <form onSubmit={e => { setImportedRecipeId(null); handleImport(e); }} className="p-4 bg-gray-100 rounded-md border border-gray-200 space-y-3">
          {importError && (
            <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">{importError}</p>
          )}
          {importedRecipeId && (
            <p className="text-sm text-green-700 bg-green-50 p-2 rounded border border-green-300">
              Recipe imported!{' '}
              <Link to={`/recipes/${importedRecipeId}`} className="font-medium underline">View recipe</Link>
            </p>
          )}
          <div className="flex gap-2">
            <input
              type="url"
              value={importUrl}
              onChange={e => { setImportUrl(e.target.value); setImportedRecipeId(null); }}
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
          {isImporting ? (
            <p className="text-xs text-gray-500 flex items-center gap-1.5">
              <svg className="animate-spin h-3 w-3 text-gray-400" viewBox="0 0 24 24" fill="none">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
              </svg>
              Fetching and parsing the recipe — this can take up to 30 seconds.
            </p>
          ) : (
            <p className="text-xs text-gray-500">
              Supports 500+ recipe sites. Results may vary for unsupported sites.
            </p>
          )}
        </form>
      )}

      {/* Import from image */}
      {mode === 'image' && !isPaid && (
        <UpgradePrompt message="Recipe import from image is a Premium feature." />
      )}
      {mode === 'image' && isPaid && (
        <form onSubmit={e => { setImportedImageRecipeId(null); handleImportFromImage(e); }} className="p-4 bg-gray-100 rounded-md border border-gray-200 space-y-3">
          {imageImportError && (
            <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">{imageImportError}</p>
          )}
          {importedImageRecipeId && (
            <p className="text-sm text-green-700 bg-green-50 p-2 rounded border border-green-300">
              Recipe imported!{' '}
              <Link to={`/recipes/${importedImageRecipeId}`} className="font-medium underline">View recipe</Link>
            </p>
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
          {isImportingImage ? (
            <p className="text-xs text-gray-500 flex items-center gap-1.5">
              <svg className="animate-spin h-3 w-3 text-gray-400" viewBox="0 0 24 24" fill="none">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
              </svg>
              Analysing the image — this can take up to 30 seconds.
            </p>
          ) : (
            <p className="text-xs text-gray-500">
              Take a photo of a recipe card, book page, or printed recipe.
            </p>
          )}
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
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-2">Ingredients</label>
            <div className="space-y-2">
              {ingredients.map((ing, i) => (
                <div key={i} className="flex gap-2">
                  <input
                    type="text"
                    value={ing.text}
                    onChange={e => updateIngredient(i, e.target.value)}
                    placeholder="e.g. 2 cups flour"
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
          {!isPaid && recipes.length >= FREE_RECIPE_LIMIT ? (
            <UpgradePrompt message="You've reached the 25-recipe limit. Upgrade to save unlimited recipes." />
          ) : (
            <button
              type="submit"
              disabled={isCreating || !title.trim()}
              aria-busy={isCreating}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-md shadow-sm hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isCreating ? 'Saving…' : 'Save Recipe'}
            </button>
          )}
        </form>
      )}

      {/* ── Tag management panel ─────────────────────────────────────────────── */}
      <div className="border border-gray-200 rounded-lg overflow-hidden">
        <button
          onClick={() => setShowTagPanel(p => !p)}
          className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-gray-100 transition-colors text-sm font-medium text-gray-700"
        >
          <span className="flex items-center gap-2">
            🏷️ Manage Tags
            {allTags.length > 0 && (
              <span className="text-xs text-gray-500">({allTags.length})</span>
            )}
          </span>
          <span className="text-gray-400 text-xs">{showTagPanel ? '▲' : '▼'}</span>
        </button>

        {showTagPanel && (
          <div className="p-4 space-y-4 bg-white">
            {/* Existing tags */}
            {allTags.length > 0 && (
              <div className="space-y-2">
                {allTags.map(tag => (
                  <div key={tag.id} className="flex items-center gap-2">
                    <TagChip tag={tag} />
                    <div className="flex-1" />
                    <button
                      onClick={() => startEditTag(tag)}
                      className="text-xs text-indigo-600 hover:text-indigo-800"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDeleteTag(tag.id)}
                      className="text-xs text-red-500 hover:text-red-700"
                    >
                      Delete
                    </button>
                  </div>
                ))}
              </div>
            )}

            {/* Create / edit form */}
            <form onSubmit={handleSaveTag} className="space-y-3 border-t border-gray-100 pt-4">
              <p className="text-xs font-medium text-gray-600">
                {editingTagId ? 'Edit tag' : 'New tag'}
              </p>
              {tagError && (
                <p className="text-xs text-red-600 bg-red-50 p-2 rounded border border-red-200">{tagError}</p>
              )}
              <div className="flex gap-2">
                <input
                  type="text"
                  value={tagName}
                  onChange={e => setTagName(e.target.value)}
                  placeholder="Tag name…"
                  required
                  maxLength={50}
                  disabled={isSavingTag}
                  className="flex-1 px-3 py-1.5 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
                />
              </div>

              {/* Color picker */}
              <div className="flex flex-wrap gap-2">
                {TAG_COLORS.map(c => (
                  <button
                    key={c.name}
                    type="button"
                    onClick={() => setTagColor(c.name)}
                    title={c.name}
                    className={`w-6 h-6 rounded-full ${c.dot} border-2 transition-all ${
                      tagColor === c.name ? 'border-gray-700 scale-110' : 'border-transparent hover:scale-110'
                    }`}
                  />
                ))}
              </div>

              {/* Preview */}
              {tagName.trim() && (
                <div className="flex items-center gap-2">
                  <span className="text-xs text-gray-500">Preview:</span>
                  <TagChip tag={{ id: '', name: tagName.trim(), color: tagColor }} />
                </div>
              )}

              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={isSavingTag || !tagName.trim()}
                  className="px-3 py-1.5 bg-indigo-600 text-white text-xs font-semibold rounded-md hover:bg-indigo-700 disabled:opacity-50 transition-colors"
                >
                  {isSavingTag ? 'Saving…' : editingTagId ? 'Update' : 'Create tag'}
                </button>
                {editingTagId && (
                  <button
                    type="button"
                    onClick={resetTagForm}
                    className="px-3 py-1.5 border border-gray-300 text-xs text-gray-600 rounded-md hover:bg-gray-50 transition-colors"
                  >
                    Cancel
                  </button>
                )}
              </div>
            </form>
          </div>
        )}
      </div>

      {/* ── Share all recipes panel ─────────────────────────────────────────── */}
      {recipes.some(r => r.isOwned) && (
        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <button
            onClick={handleOpenShareAll}
            className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-gray-100 transition-colors text-sm font-medium text-gray-700"
          >
            <span>Share All Recipes</span>
            <span className="text-gray-400 text-xs">{showShareAllPanel ? '▲' : '▼'}</span>
          </button>

          {showShareAllPanel && (
            <div className="p-4 bg-white space-y-3">
              {isShareAllLoading && <p className="text-sm text-gray-500">Loading families…</p>}

              {!isShareAllLoading && shareAllFamilies.length === 0 && (
                <p className="text-sm text-gray-500">Create a family first to share your recipes.</p>
              )}

              {!isShareAllLoading && shareAllFamilies.length > 0 && (
                <form onSubmit={handleShareAll} className="space-y-3">
                  <p className="text-xs text-gray-500">
                    Share all your owned recipes with a family. Recipes already shared are skipped.
                  </p>
                  {shareAllError && (
                    <p className="text-xs text-red-600 bg-red-50 p-2 rounded border border-red-200">{shareAllError}</p>
                  )}
                  {shareAllResult && (
                    <p className="text-xs text-green-700 bg-green-50 p-2 rounded border border-green-200">
                      Shared {shareAllResult.sharedCount} recipe{shareAllResult.sharedCount !== 1 ? 's' : ''} with {shareAllResult.familyName}
                      {shareAllResult.skippedCount > 0 && ` (${shareAllResult.skippedCount} already shared)`}.
                    </p>
                  )}
                  <div className="flex gap-2">
                    <select
                      value={shareAllFamilyId}
                      onChange={e => { setShareAllFamilyId(e.target.value); setShareAllResult(null); }}
                      disabled={isShareAllSubmitting}
                      className="flex-grow text-sm px-2 py-1.5 border border-gray-300 rounded focus:outline-none focus:ring-indigo-500 disabled:bg-gray-100"
                    >
                      {shareAllFamilies.map(f => (
                        <option key={f.id} value={f.id}>{f.name}</option>
                      ))}
                    </select>
                    <button
                      type="submit"
                      disabled={isShareAllSubmitting || !shareAllFamilyId}
                      aria-busy={isShareAllSubmitting}
                      className="px-3 py-1.5 bg-indigo-600 text-white text-sm font-semibold rounded hover:bg-indigo-700 disabled:opacity-50 transition-colors"
                    >
                      {isShareAllSubmitting ? 'Sharing…' : 'Share All'}
                    </button>
                  </div>
                </form>
              )}
            </div>
          )}
        </div>
      )}

      {/* ── Tag filter chips ─────────────────────────────────────────────────── */}
      {allTags.length > 0 && (
        <div className="flex flex-wrap gap-2 items-center">
          <span className="text-xs text-gray-500 font-medium">Filter:</span>
          {allTags.map(tag => {
            const active = filterTagIds.has(tag.id);
            const c = getTagColor(tag.color);
            return (
              <button
                key={tag.id}
                onClick={() => toggleFilterTag(tag.id)}
                className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border transition-all ${
                  active
                    ? `${c.bg} ${c.text} border-current ring-1 ring-current`
                    : 'bg-white text-gray-500 border-gray-300 hover:border-gray-400'
                }`}
              >
                <span className={`w-1.5 h-1.5 rounded-full ${active ? c.dot : 'bg-gray-300'}`} />
                {tag.name}
              </button>
            );
          })}
          {filterTagIds.size > 0 && (
            <button
              onClick={() => setFilterTagIds(new Set())}
              className="text-xs text-gray-400 hover:text-gray-600 underline"
            >
              Clear filters
            </button>
          )}
        </div>
      )}

      {/* Search + sort */}
      {!isLoading && !error && recipes.length > 0 && (
        <div className="flex gap-2">
          <input
            type="search"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="Search recipes…"
            className="flex-1 px-3 py-2 border border-gray-300 rounded-md text-sm shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500"
          />
          <div className="flex rounded-md border border-gray-300 overflow-hidden text-sm shadow-sm">
            <button
              onClick={() => setSortOrder('date')}
              className={`px-3 py-2 transition-colors ${sortOrder === 'date' ? 'bg-indigo-600 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
              title="Sort by newest first"
            >
              Newest
            </button>
            <button
              onClick={() => setSortOrder('alpha')}
              className={`px-3 py-2 border-l border-gray-300 transition-colors ${sortOrder === 'alpha' ? 'bg-indigo-600 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
              title="Sort alphabetically"
            >
              A–Z
            </button>
          </div>
        </div>
      )}

      {/* Recipe list */}
      {isLoading && <p className="text-gray-500">Loading recipes…</p>}
      {error && <p className="text-red-600">{error}</p>}
      {!isLoading && !error && recipes.length === 0 && (
        <p className="text-gray-500">No recipes yet. Import one or create one above!</p>
      )}
      {!isLoading && !error && recipes.length > 0 && visibleRecipes.length === 0 && (
        <p className="text-gray-500">No recipes match the current filters.</p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {visibleRecipes.map(recipe => (
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
              <div className="text-xs text-gray-500 space-x-3 mb-2">
                {recipe.yields && <span>{recipe.yields}</span>}
                {recipe.totalTimeMinutes != null && <span>{recipe.totalTimeMinutes} min</span>}
                <span>{recipe.ingredientCount} ingredients</span>
              </div>

              {/* Tags */}
              {recipe.tags.length > 0 && (
                <div className="flex flex-wrap gap-1 mb-3">
                  {recipe.tags.map(tag => <TagChip key={tag.id} tag={tag} />)}
                </div>
              )}

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
                      {activeLists.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
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
                  activeLists.length > 0 && (
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
    </div>
  );
};

export default RecipesPage;
