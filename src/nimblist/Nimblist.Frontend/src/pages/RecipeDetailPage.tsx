import React, { useEffect, useState, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { authenticatedFetch } from '../components/HttpHelper';
import { RecipeDetail, ShoppingList } from '../types/index';
import SharePanel from '../components/SharePanel';
import ImageSearchModal from '../components/ImageSearchModal';
import { transformQuantity, hasAnyImperialUnit } from '../utils/ingredientScaling';

interface EditIngredient {
  text: string;
  parsedName: string | null;
  parsedQuantity: string | null;
}

const RecipeDetailPage: React.FC = () => {
  const { recipeId } = useParams<{ recipeId: string }>();
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [lists, setLists] = useState<ShoppingList[]>([]);
  const [selectedListId, setSelectedListId] = useState('');
  const [isAdding, setIsAdding] = useState(false);
  const [addResult, setAddResult] = useState<string | null>(null);
  const [addError, setAddError] = useState<string | null>(null);

  // Scaling state
  const [scaleFactor, setScaleFactor] = useState(1);
  const [customScale, setCustomScale] = useState('');
  const [useMetric, setUseMetric] = useState(false);

  // Edit mode
  const [isEditing, setIsEditing] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editYields, setEditYields] = useState('');
  const [editTotalTime, setEditTotalTime] = useState('');
  const [editImageUrl, setEditImageUrl] = useState('');
  const [editInstructions, setEditInstructions] = useState('');
  const [editIngredients, setEditIngredients] = useState<EditIngredient[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [showImageSearch, setShowImageSearch] = useState(false);

  useEffect(() => {
    if (!recipeId) return;
    Promise.all([
      authenticatedFetch(`/api/recipes/${recipeId}`).then(r => r.json()),
      authenticatedFetch('/api/shoppinglists').then(r => r.json()),
    ])
      .then(([recipeData, listsData]) => {
        setRecipe(recipeData);
        setLists(listsData);
        if (listsData.length > 0) setSelectedListId(listsData[0].id);
      })
      .catch(() => setError('Failed to load recipe.'))
      .finally(() => setIsLoading(false));
  }, [recipeId]);

  const enterEditMode = () => {
    if (!recipe) return;
    setEditTitle(recipe.title);
    setEditDescription(recipe.description ?? '');
    setEditYields(recipe.yields ?? '');
    setEditTotalTime(recipe.totalTimeMinutes != null ? String(recipe.totalTimeMinutes) : '');
    setEditInstructions(recipe.instructions ?? '');
    setEditImageUrl(recipe.imageUrl ?? '');
    setEditIngredients(
      recipe.ingredients.length > 0
        ? recipe.ingredients.map(i => ({ text: i.text, parsedName: i.parsedName, parsedQuantity: i.parsedQuantity }))
        : [{ text: '', parsedName: null, parsedQuantity: null }]
    );
    setSaveError(null);
    setIsEditing(true);
  };

  const cancelEdit = () => setIsEditing(false);

  const updateIngredient = (index: number, value: string) => {
    setEditIngredients(prev => prev.map((ing, i) =>
      i === index
        ? { text: value, parsedName: value === recipe?.ingredients[i]?.text ? ing.parsedName : null, parsedQuantity: value === recipe?.ingredients[i]?.text ? ing.parsedQuantity : null }
        : ing
    ));
  };

  const addIngredientRow = () => setEditIngredients(prev => [...prev, { text: '', parsedName: null, parsedQuantity: null }]);

  const removeIngredientRow = (index: number) => {
    if (editIngredients.length === 1) return;
    setEditIngredients(prev => prev.filter((_, i) => i !== index));
  };

  const handleSave = async () => {
    if (!recipeId || !editTitle.trim()) return;
    setIsSaving(true);
    setSaveError(null);
    const validIngredients = editIngredients.filter(i => i.text.trim());
    try {
      const response = await authenticatedFetch(`/api/recipes/${recipeId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title: editTitle.trim(),
          description: editDescription.trim() || null,
          sourceUrl: recipe?.sourceUrl ?? null,
          imageUrl: editImageUrl.trim() || null,
          yields: editYields.trim() || null,
          totalTimeMinutes: editTotalTime ? parseInt(editTotalTime) : null,
          instructions: editInstructions.trim() || null,
          ingredients: validIngredients.map((ing, i) => ({
            text: ing.text.trim(),
            parsedName: ing.parsedName,
            parsedQuantity: ing.parsedQuantity,
            sortOrder: i,
          })),
        }),
      });
      if (response.ok) {
        const updated: RecipeDetail = await response.json();
        setRecipe(updated);
        setIsEditing(false);
      } else {
        const body = await response.json().catch(() => null);
        setSaveError(body?.title ?? `Failed to save (${response.status})`);
      }
    } catch {
      setSaveError('Network error — could not save changes.');
    } finally {
      setIsSaving(false);
    }
  };

  const scaledQuantities = useMemo(() => {
    if (!recipe) return {};
    return Object.fromEntries(
      recipe.ingredients.map(ing => [
        ing.id,
        transformQuantity(ing.parsedQuantity, scaleFactor, useMetric),
      ])
    );
  }, [recipe, scaleFactor, useMetric]);

  const showMetricToggle = useMemo(
    () => recipe ? hasAnyImperialUnit(recipe.ingredients.map(i => i.parsedQuantity)) : false,
    [recipe]
  );

  const isScaled = scaleFactor !== 1 || useMetric;

  const handleAddToList = async () => {
    if (!selectedListId || !recipeId) return;
    setIsAdding(true);
    setAddResult(null);
    setAddError(null);
    try {
      const body = isScaled
        ? JSON.stringify({ quantityOverrides: scaledQuantities })
        : undefined;
      const response = await authenticatedFetch(
        `/api/recipes/${recipeId}/addtolist/${selectedListId}`,
        {
          method: 'POST',
          headers: body ? { 'Content-Type': 'application/json' } : undefined,
          body,
        }
      );
      if (response.ok) {
        const data = await response.json();
        setAddResult(`Added ${data.addedCount} ingredient${data.addedCount !== 1 ? 's' : ''} to list.`);
      } else {
        setAddError('Failed to add ingredients to list.');
      }
    } catch {
      setAddError('Network error adding ingredients.');
    } finally {
      setIsAdding(false);
    }
  };

  if (isLoading) return <p className="text-gray-500 mt-4">Loading recipe…</p>;
  if (error || !recipe) return (
    <div>
      <Link to="/recipes" className="text-sm text-blue-600 hover:underline">&larr; Back to Recipes</Link>
      <p className="mt-4 text-red-600">{error ?? 'Recipe not found.'}</p>
    </div>
  );

  const steps = recipe.instructions
    ? recipe.instructions.split('\n').filter(s => s.trim())
    : [];

  return (
    <div className="space-y-6 max-w-3xl">
      <Link to="/recipes" className="text-sm text-blue-600 hover:underline">&larr; Back to Recipes</Link>

      <ImageSearchModal
        isOpen={showImageSearch}
        onClose={() => setShowImageSearch(false)}
        onSelect={url => setEditImageUrl(url)}
        initialQuery={editTitle}
      />

      {isEditing ? (
        /* ── Edit form ── */
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-bold text-gray-800">Edit Recipe</h2>
            <button onClick={cancelEdit} className="text-sm text-gray-500 hover:text-gray-700">Cancel</button>
          </div>

          {saveError && <p className="text-sm text-red-600 bg-red-50 p-2 rounded border border-red-200">{saveError}</p>}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="sm:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">Title *</label>
              <input
                type="text"
                value={editTitle}
                onChange={e => setEditTitle(e.target.value)}
                required
                disabled={isSaving}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Yields</label>
              <input
                type="text"
                value={editYields}
                onChange={e => setEditYields(e.target.value)}
                placeholder="e.g. 4 servings"
                disabled={isSaving}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Total time (minutes)</label>
              <input
                type="number"
                min="1"
                value={editTotalTime}
                onChange={e => setEditTotalTime(e.target.value)}
                disabled={isSaving}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">Description</label>
              <textarea
                value={editDescription}
                onChange={e => setEditDescription(e.target.value)}
                rows={2}
                disabled={isSaving}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">Image URL</label>
              {editImageUrl && (
                <img
                  src={editImageUrl}
                  alt="Recipe preview"
                  className="mb-2 max-h-40 rounded-md border border-gray-200 object-cover"
                  onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
                />
              )}
              <div className="flex gap-2">
                <input
                  type="url"
                  value={editImageUrl}
                  onChange={e => setEditImageUrl(e.target.value)}
                  placeholder="https://example.com/image.jpg"
                  disabled={isSaving}
                  className="flex-grow px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
                />
                <button
                  type="button"
                  onClick={() => setShowImageSearch(true)}
                  disabled={isSaving}
                  title="Search Google Images"
                  className="px-3 py-2 text-sm border border-gray-300 rounded-md text-gray-600 hover:bg-gray-50 hover:border-indigo-400 disabled:opacity-30 transition-colors flex items-center gap-1"
                >
                  🔍 Find image
                </button>
                {editImageUrl && (
                  <button
                    type="button"
                    onClick={() => setEditImageUrl('')}
                    disabled={isSaving}
                    className="px-2 text-gray-400 hover:text-red-500 disabled:opacity-30 transition-colors"
                    aria-label="Clear image"
                  >
                    ✕
                  </button>
                )}
              </div>
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-2">Ingredients</label>
            <div className="space-y-2">
              {editIngredients.map((ing, i) => (
                <div key={i} className="flex gap-2">
                  <input
                    type="text"
                    value={ing.text}
                    onChange={e => updateIngredient(i, e.target.value)}
                    placeholder="e.g. 2 cups flour"
                    disabled={isSaving}
                    className="flex-grow px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
                  />
                  <button
                    type="button"
                    onClick={() => removeIngredientRow(i)}
                    disabled={editIngredients.length === 1 || isSaving}
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
              disabled={isSaving}
              className="mt-2 text-sm text-indigo-600 hover:text-indigo-800 disabled:opacity-50"
            >
              + Add ingredient
            </button>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Instructions</label>
            <textarea
              value={editInstructions}
              onChange={e => setEditInstructions(e.target.value)}
              rows={6}
              placeholder="Enter each step on a new line"
              disabled={isSaving}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-100"
            />
          </div>

          <div className="flex gap-3">
            <button
              onClick={handleSave}
              disabled={isSaving || !editTitle.trim()}
              aria-busy={isSaving}
              className="px-4 py-2 bg-indigo-600 text-white text-sm font-semibold rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isSaving ? 'Saving…' : 'Save changes'}
            </button>
            <button
              onClick={cancelEdit}
              disabled={isSaving}
              className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        /* ── View mode ── */
        <>
          {/* Header */}
          <div className="flex gap-6 items-start">
            {recipe.imageUrl && (
              <img
                src={recipe.imageUrl}
                alt={recipe.title}
                className="w-40 h-40 object-cover rounded-lg shadow flex-shrink-0"
                onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
              />
            )}
            <div className="flex-1">
              <div className="flex items-start justify-between gap-3">
                <h2 className="text-2xl font-bold text-gray-800">{recipe.title}</h2>
                {recipe.isOwned && (
                  <button
                    onClick={enterEditMode}
                    className="flex-shrink-0 text-sm text-indigo-600 hover:text-indigo-800 border border-indigo-200 hover:bg-indigo-50 px-3 py-1 rounded transition-colors"
                  >
                    Edit
                  </button>
                )}
              </div>
              <div className="mt-1 flex flex-wrap gap-3 text-sm text-gray-500">
                {recipe.yields && <span>Yields: {recipe.yields}</span>}
                {recipe.totalTimeMinutes != null && <span>Total time: {recipe.totalTimeMinutes} min</span>}
                {recipe.sourceUrl && (
                  <a href={recipe.sourceUrl} target="_blank" rel="noopener noreferrer"
                    className="text-blue-500 hover:underline">
                    Source
                  </a>
                )}
              </div>
              {recipe.description && (
                <p className="mt-2 text-sm text-gray-600">{recipe.description}</p>
              )}
            </div>
          </div>

          {/* Add to list */}
          <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg space-y-2">
            <h3 className="font-semibold text-gray-700">Add ingredients to a shopping list</h3>
            {addResult && <p className="text-sm text-green-700 bg-green-50 p-2 rounded">{addResult}</p>}
            {addError && <p className="text-sm text-red-600 bg-red-50 p-2 rounded">{addError}</p>}
            {lists.length === 0 ? (
              <p className="text-sm text-gray-500">
                No lists yet. <Link to="/lists" className="text-blue-500 hover:underline">Create one</Link> first.
              </p>
            ) : (
              <div className="flex gap-2">
                <select
                  value={selectedListId}
                  onChange={e => setSelectedListId(e.target.value)}
                  className="flex-grow px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500"
                  disabled={isAdding}
                >
                  {lists.map(l => (
                    <option key={l.id} value={l.id}>{l.name}</option>
                  ))}
                </select>
                <button
                  onClick={handleAddToList}
                  disabled={isAdding || !selectedListId}
                  aria-busy={isAdding}
                  className="px-4 py-2 bg-green-600 text-white text-sm font-semibold rounded-md hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  {isAdding ? 'Adding…' : 'Add to list'}
                </button>
              </div>
            )}
          </div>

          {/* Sharing */}
          {recipeId && (
            <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg space-y-2">
              <h3 className="font-semibold text-gray-700">Sharing</h3>
              <SharePanel
                endpoint={`/api/recipeshares?recipeId=${recipeId}`}
                postEndpoint="/api/recipeshares"
                resourceId={recipeId}
                resourceKey="recipeId"
                isOwner={recipe.isOwned}
              />
            </div>
          )}

          {/* Scale / Unit controls */}
          <div className="p-4 bg-indigo-50 border border-indigo-200 rounded-lg space-y-3">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <h3 className="font-semibold text-gray-700 text-sm">Scale recipe</h3>
              {isScaled && (
                <span className="text-xs text-indigo-600 font-medium bg-indigo-100 px-2 py-0.5 rounded-full">Scaled</span>
              )}
            </div>
            <div className="flex flex-wrap gap-2 items-center">
              {([0.5, 1, 2, 3, 4] as const).map(factor => (
                <button
                  key={factor}
                  onClick={() => { setScaleFactor(factor); setCustomScale(''); }}
                  className={`px-3 py-1 text-sm rounded-md border transition-colors ${
                    scaleFactor === factor && customScale === ''
                      ? 'bg-indigo-600 text-white border-indigo-600'
                      : 'bg-white text-gray-700 border-gray-300 hover:border-indigo-400'
                  }`}
                >
                  {factor === 0.5 ? '½×' : `${factor}×`}
                </button>
              ))}
              <div className="flex items-center gap-1">
                <input
                  type="number"
                  min="0.1"
                  step="0.1"
                  value={customScale}
                  placeholder="Custom"
                  onChange={e => {
                    const val = e.target.value;
                    setCustomScale(val);
                    const n = parseFloat(val);
                    if (!isNaN(n) && n > 0) setScaleFactor(n);
                  }}
                  className="w-20 px-2 py-1 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500"
                />
                <span className="text-sm text-gray-500">×</span>
              </div>
              {showMetricToggle && (
                <button
                  onClick={() => setUseMetric(m => !m)}
                  className={`px-3 py-1 text-sm rounded-md border transition-colors ${
                    useMetric
                      ? 'bg-teal-600 text-white border-teal-600'
                      : 'bg-white text-gray-700 border-gray-300 hover:border-teal-400'
                  }`}
                >
                  Metric
                </button>
              )}
            </div>
          </div>

          {/* Ingredients */}
          <div>
            <h3 className="text-lg font-semibold text-gray-800 mb-2">
              Ingredients ({recipe.ingredients.length})
            </h3>
            <ul className="space-y-1">
              {recipe.ingredients.map(ing => {
                const displayQty = scaledQuantities[ing.id] ?? ing.parsedQuantity;
                return (
                  <li key={ing.id} className="flex items-start gap-2 text-sm text-gray-700">
                    <span className="mt-1.5 w-1.5 h-1.5 rounded-full bg-gray-400 flex-shrink-0" />
                    {displayQty && (
                      <span className={`font-medium flex-shrink-0 ${isScaled ? 'text-indigo-600' : 'text-gray-500'}`}>
                        {displayQty}
                      </span>
                    )}
                    <span>{ing.parsedName ?? ing.text}</span>
                  </li>
                );
              })}
            </ul>
          </div>

          {/* Instructions */}
          {steps.length > 0 && (
            <div>
              <h3 className="text-lg font-semibold text-gray-800 mb-2">Instructions</h3>
              <ol className="space-y-3">
                {steps.map((step, i) => (
                  <li key={i} className="flex gap-3 text-sm text-gray-700">
                    <span className="flex-shrink-0 w-6 h-6 rounded-full bg-indigo-100 text-indigo-700 text-xs font-bold flex items-center justify-center">
                      {i + 1}
                    </span>
                    <span>{step}</span>
                  </li>
                ))}
              </ol>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default RecipeDetailPage;
