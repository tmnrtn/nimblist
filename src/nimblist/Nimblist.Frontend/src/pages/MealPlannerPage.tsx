import React, { useEffect, useState, useCallback } from 'react';
import { authenticatedFetch } from '../components/HttpHelper';
import { MealPlanSummary, MealPlanEntry, RecipeSummary, ShoppingList } from '../types/index';
import SharePanel from '../components/SharePanel';

const MEAL_TYPES = ['Breakfast', 'Lunch', 'Dinner', 'Snack', 'Other'];

function getMonday(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

function addDays(date: Date, n: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + n);
  return d;
}

function toISODate(date: Date): string {
  return date.toISOString().split('T')[0];
}

function formatDay(date: Date): string {
  return date.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
}

function formatWeekLabel(monday: Date): string {
  const sunday = addDays(monday, 6);
  return `${monday.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })} – ${sunday.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}`;
}

const MealPlannerPage: React.FC = () => {
  const [plans, setPlans] = useState<MealPlanSummary[]>([]);
  const [selectedPlanId, setSelectedPlanId] = useState('');
  const [entries, setEntries] = useState<MealPlanEntry[]>([]);
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [lists, setLists] = useState<ShoppingList[]>([]);
  const [weekStart, setWeekStart] = useState<Date>(() => getMonday(new Date()));
  const [isLoading, setIsLoading] = useState(true);
  const [showSharePanel, setShowSharePanel] = useState(false);

  // New plan form
  const [newPlanName, setNewPlanName] = useState('');
  const [isCreatingPlan, setIsCreatingPlan] = useState(false);
  const [showNewPlanForm, setShowNewPlanForm] = useState(false);

  // Add entry inline state
  const [addingToDay, setAddingToDay] = useState<string | null>(null);
  const [addRecipeId, setAddRecipeId] = useState('');
  const [addMealType, setAddMealType] = useState('Dinner');
  const [isAddingEntry, setIsAddingEntry] = useState(false);

  // Add-to-list inline state (per entry)
  const [entryToAddToList, setEntryToAddToList] = useState<string | null>(null);
  const [addToListId, setAddToListId] = useState('');
  const [isAddingToList, setIsAddingToList] = useState(false);
  const [addToListResult, setAddToListResult] = useState<{ entryId: string; message: string } | null>(null);

  // Load plans, recipes, lists on mount
  useEffect(() => {
    Promise.all([
      authenticatedFetch('/api/mealplans').then(r => r.json()),
      authenticatedFetch('/api/recipes').then(r => r.json()),
      authenticatedFetch('/api/shoppinglists').then(r => r.json()),
    ]).then(([plansData, recipesData, listsData]) => {
      setPlans(plansData);
      setRecipes(recipesData);
      setLists(listsData);
      if (plansData.length > 0) setSelectedPlanId(plansData[0].id);
      if (listsData.length > 0) setAddToListId(listsData[0].id);
      if (recipesData.length > 0) setAddRecipeId(recipesData[0].id);
    }).catch(() => {})
      .finally(() => setIsLoading(false));
  }, []);

  // Load entries whenever plan or week changes
  const loadEntries = useCallback(async () => {
    if (!selectedPlanId) { setEntries([]); return; }
    const from = toISODate(weekStart);
    const to = toISODate(addDays(weekStart, 6));
    try {
      const data = await authenticatedFetch(`/api/mealplans/${selectedPlanId}/entries?from=${from}&to=${to}`)
        .then(r => r.ok ? r.json() : []);
      setEntries(data);
    } catch { setEntries([]); }
  }, [selectedPlanId, weekStart]);

  useEffect(() => { loadEntries(); }, [loadEntries]);

  const handleCreatePlan = async () => {
    if (!newPlanName.trim()) return;
    setIsCreatingPlan(true);
    try {
      const response = await authenticatedFetch('/api/mealplans', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: newPlanName.trim() }),
      });
      if (response.ok) {
        const plan: MealPlanSummary = await response.json();
        setPlans(prev => [plan, ...prev]);
        setSelectedPlanId(plan.id);
        setNewPlanName('');
        setShowNewPlanForm(false);
      }
    } catch { /* ignore */ }
    finally { setIsCreatingPlan(false); }
  };

  const handleAddEntry = async (date: string) => {
    if (!selectedPlanId || !addRecipeId) return;
    setIsAddingEntry(true);
    try {
      const response = await authenticatedFetch('/api/mealplanentries', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          mealPlanId: selectedPlanId,
          recipeId: addRecipeId,
          plannedDate: date,
          mealType: addMealType || null,
          notes: null,
        }),
      });
      if (response.ok) {
        const entry: MealPlanEntry = await response.json();
        setEntries(prev => [...prev, entry]);
        setAddingToDay(null);
      }
    } catch { /* ignore */ }
    finally { setIsAddingEntry(false); }
  };

  const handleDeleteEntry = async (entryId: string) => {
    setEntries(prev => prev.filter(e => e.id !== entryId));
    try {
      await authenticatedFetch(`/api/mealplanentries/${entryId}`, { method: 'DELETE' });
    } catch {
      await loadEntries();
    }
  };

  const handleAddToList = async (entryId: string) => {
    if (!addToListId) return;
    setIsAddingToList(true);
    try {
      const response = await authenticatedFetch(
        `/api/mealplanentries/${entryId}/addtolist/${addToListId}`,
        { method: 'POST' }
      );
      if (response.ok) {
        const data = await response.json();
        setAddToListResult({ entryId, message: `Added ${data.addedCount} ingredient${data.addedCount !== 1 ? 's' : ''}` });
        setEntryToAddToList(null);
        setTimeout(() => setAddToListResult(null), 3000);
      }
    } catch { /* ignore */ }
    finally { setIsAddingToList(false); }
  };

  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const selectedPlan = plans.find(p => p.id === selectedPlanId);

  if (isLoading) return <p className="text-gray-500 mt-4">Loading meal planner…</p>;

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-2xl font-bold text-gray-800">Meal Planner</h2>

        {plans.length > 0 && (
          <select
            value={selectedPlanId}
            onChange={e => setSelectedPlanId(e.target.value)}
            className="px-3 py-1.5 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500"
          >
            {plans.map(p => (
              <option key={p.id} value={p.id}>{p.name}{!p.isOwned ? ' (shared)' : ''}</option>
            ))}
          </select>
        )}

        <button
          onClick={() => setShowNewPlanForm(v => !v)}
          className="text-sm px-3 py-1.5 border border-indigo-300 text-indigo-600 rounded-md hover:bg-indigo-50 transition-colors"
        >
          + New Plan
        </button>

        {selectedPlan && (
          <button
            onClick={() => setShowSharePanel(v => !v)}
            className="ml-auto text-sm px-3 py-1.5 border border-gray-300 text-gray-600 rounded-md hover:bg-gray-50 transition-colors"
          >
            {showSharePanel ? 'Hide sharing' : 'Share'}
          </button>
        )}
      </div>

      {/* New plan form */}
      {showNewPlanForm && (
        <div className="flex gap-2 p-3 bg-gray-50 border border-gray-200 rounded-md">
          <input
            type="text"
            value={newPlanName}
            onChange={e => setNewPlanName(e.target.value)}
            placeholder="Plan name"
            className="flex-grow px-3 py-1.5 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500"
            onKeyDown={e => e.key === 'Enter' && handleCreatePlan()}
          />
          <button
            onClick={handleCreatePlan}
            disabled={isCreatingPlan || !newPlanName.trim()}
            className="px-3 py-1.5 text-sm bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            {isCreatingPlan ? '…' : 'Create'}
          </button>
        </div>
      )}

      {/* Share panel */}
      {showSharePanel && selectedPlan && (
        <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg">
          <h3 className="text-sm font-semibold text-gray-700 mb-2">Sharing — {selectedPlan.name}</h3>
          <SharePanel
            endpoint={`/api/mealplanshares?mealPlanId=${selectedPlanId}`}
            postEndpoint="/api/mealplanshares"
            resourceId={selectedPlanId}
            resourceKey="mealPlanId"
            isOwner={selectedPlan.isOwned}
          />
        </div>
      )}

      {plans.length === 0 ? (
        <p className="text-gray-500">No meal plans yet. Create one above to get started.</p>
      ) : (
        <>
          {/* Week navigation */}
          <div className="flex items-center gap-3">
            <button
              onClick={() => setWeekStart(d => addDays(d, -7))}
              className="px-3 py-1.5 text-sm border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
            >
              ← Prev
            </button>
            <span className="text-sm font-medium text-gray-700 flex-1 text-center">
              {formatWeekLabel(weekStart)}
            </span>
            <button
              onClick={() => setWeekStart(getMonday(new Date()))}
              className="px-3 py-1.5 text-sm border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
            >
              Today
            </button>
            <button
              onClick={() => setWeekStart(d => addDays(d, 7))}
              className="px-3 py-1.5 text-sm border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
            >
              Next →
            </button>
          </div>

          {/* Calendar grid */}
          <div className="grid grid-cols-7 gap-1">
            {days.map(day => {
              const dateStr = toISODate(day);
              const isToday = dateStr === toISODate(new Date());
              const dayEntries = entries.filter(e => e.plannedDate === dateStr);
              const isAddingHere = addingToDay === dateStr;

              return (
                <div
                  key={dateStr}
                  className={`border rounded-lg p-2 min-h-32 flex flex-col gap-1 text-xs ${isToday ? 'border-indigo-400 bg-indigo-50' : 'border-gray-200 bg-white'}`}
                >
                  <div className={`font-semibold mb-1 ${isToday ? 'text-indigo-700' : 'text-gray-600'}`}>
                    {formatDay(day)}
                  </div>

                  {/* Entries */}
                  {dayEntries.map(entry => (
                    <div key={entry.id} className="bg-white border border-gray-200 rounded p-1 space-y-0.5">
                      <div className="flex items-start justify-between gap-1">
                        <span className="font-medium text-gray-800 leading-tight line-clamp-2 flex-1">
                          {entry.mealType && (
                            <span className="text-gray-400 mr-0.5">{entry.mealType.charAt(0).toUpperCase()}: </span>
                          )}
                          {entry.recipeTitle}
                        </span>
                        <button
                          onClick={() => handleDeleteEntry(entry.id)}
                          className="text-gray-300 hover:text-red-500 flex-shrink-0 transition-colors"
                          title="Remove"
                        >
                          ✕
                        </button>
                      </div>

                      {/* Add to list button */}
                      {addToListResult?.entryId === entry.id ? (
                        <p className="text-green-600 text-xs">{addToListResult.message}</p>
                      ) : entryToAddToList === entry.id ? (
                        <div className="flex gap-1 mt-1">
                          <select
                            value={addToListId}
                            onChange={e => setAddToListId(e.target.value)}
                            className="flex-1 text-xs px-1 py-0.5 border border-gray-300 rounded"
                            disabled={isAddingToList}
                          >
                            {lists.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
                          </select>
                          <button
                            onClick={() => handleAddToList(entry.id)}
                            disabled={isAddingToList}
                            className="text-xs px-1.5 py-0.5 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50"
                          >
                            {isAddingToList ? '…' : '→'}
                          </button>
                          <button
                            onClick={() => setEntryToAddToList(null)}
                            className="text-xs text-gray-400 hover:text-gray-600"
                          >
                            ✕
                          </button>
                        </div>
                      ) : (
                        lists.length > 0 && (
                          <button
                            onClick={() => { setEntryToAddToList(entry.id); setAddingToDay(null); }}
                            className="text-indigo-500 hover:text-indigo-700 transition-colors"
                            title="Add ingredients to shopping list"
                          >
                            → Add to list
                          </button>
                        )
                      )}
                    </div>
                  ))}

                  {/* Add entry form */}
                  {isAddingHere ? (
                    <div className="mt-1 space-y-1">
                      <select
                        value={addRecipeId}
                        onChange={e => setAddRecipeId(e.target.value)}
                        className="w-full text-xs px-1.5 py-1 border border-gray-300 rounded focus:outline-none focus:ring-indigo-500"
                        disabled={isAddingEntry}
                      >
                        {recipes.map(r => <option key={r.id} value={r.id}>{r.title}</option>)}
                      </select>
                      <select
                        value={addMealType}
                        onChange={e => setAddMealType(e.target.value)}
                        className="w-full text-xs px-1.5 py-1 border border-gray-300 rounded focus:outline-none focus:ring-indigo-500"
                        disabled={isAddingEntry}
                      >
                        {MEAL_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                      </select>
                      <div className="flex gap-1">
                        <button
                          onClick={() => handleAddEntry(dateStr)}
                          disabled={isAddingEntry || !addRecipeId}
                          className="flex-1 text-xs py-0.5 bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50"
                        >
                          {isAddingEntry ? '…' : 'Add'}
                        </button>
                        <button
                          onClick={() => setAddingToDay(null)}
                          className="text-xs px-1.5 py-0.5 border border-gray-300 rounded hover:bg-gray-50"
                        >
                          ✕
                        </button>
                      </div>
                    </div>
                  ) : (
                    recipes.length > 0 && (
                      <button
                        onClick={() => { setAddingToDay(dateStr); setEntryToAddToList(null); }}
                        className="mt-auto text-center text-indigo-500 hover:text-indigo-700 border border-dashed border-indigo-300 rounded py-0.5 transition-colors"
                      >
                        + Add
                      </button>
                    )
                  )}
                </div>
              );
            })}
          </div>

          {recipes.length === 0 && (
            <p className="text-sm text-gray-500 text-center">
              Import or create recipes first to start planning meals.
            </p>
          )}
        </>
      )}
    </div>
  );
};

export default MealPlannerPage;
