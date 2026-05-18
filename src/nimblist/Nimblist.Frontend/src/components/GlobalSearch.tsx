import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authenticatedFetch } from './HttpHelper';
import type { RecipeSummary, ShoppingList } from '../types';

type ResultType = 'list' | 'recipe';
interface SearchResult { type: ResultType; id: string; title: string; }

const GlobalSearch: React.FC = () => {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [lists, setLists] = useState<ShoppingList[]>([]);
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  const close = useCallback(() => { setOpen(false); setQuery(''); setActiveIndex(-1); }, []);

  useEffect(() => {
    if (!open) return;
    setLoading(true);
    Promise.all([
      authenticatedFetch('/api/shoppinglists').then(r => r.ok ? r.json() as Promise<ShoppingList[]> : []),
      authenticatedFetch('/api/recipes').then(r => r.ok ? r.json() as Promise<RecipeSummary[]> : []),
    ]).then(([l, r]) => { setLists(l); setRecipes(r); })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') close(); };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open, close]);

  useEffect(() => {
    if (open) { setQuery(''); setActiveIndex(-1); setTimeout(() => inputRef.current?.focus(), 0); }
  }, [open]);

  const q = query.trim().toLowerCase();
  const matchedLists = q ? lists.filter(l => !l.isTemplate && l.name.toLowerCase().includes(q)) : [];
  const matchedRecipes = q ? recipes.filter(r => r.title.toLowerCase().includes(q)) : [];
  const results: SearchResult[] = [
    ...matchedLists.map(l => ({ type: 'list' as const, id: l.id, title: l.name })),
    ...matchedRecipes.map(r => ({ type: 'recipe' as const, id: r.id, title: r.title })),
  ];

  const go = (result: SearchResult) => {
    navigate(result.type === 'list' ? `/lists/${result.id}` : `/recipes/${result.id}`);
    close();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActiveIndex(i => Math.min(i + 1, results.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveIndex(i => Math.max(i - 1, -1)); }
    else if (e.key === 'Enter' && activeIndex >= 0) { e.preventDefault(); go(results[activeIndex]); }
  };

  const SearchIcon = () => (
    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
    </svg>
  );

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        aria-label="Search lists and recipes"
        className="text-white/80 hover:text-white transition-colors p-1"
      >
        <SearchIcon />
      </button>

      {open && (
        <div
          className="fixed inset-0 z-50 bg-black/40 flex items-start justify-center pt-16 sm:pt-24 px-4"
          onClick={close}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Search"
            className="bg-white rounded-xl shadow-2xl w-full max-w-lg overflow-hidden"
            onClick={e => e.stopPropagation()}
          >
            {/* Input row */}
            <div className="flex items-center px-4 border-b border-gray-100">
              <svg className="w-5 h-5 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
              <input
                ref={inputRef}
                type="search"
                value={query}
                onChange={e => { setQuery(e.target.value); setActiveIndex(-1); }}
                onKeyDown={handleKeyDown}
                placeholder="Search lists and recipes…"
                aria-label="Search lists and recipes"
                className="flex-1 px-3 py-4 text-sm text-gray-800 placeholder-gray-400 focus:outline-none bg-transparent"
              />
              {loading && <span className="text-xs text-gray-400 mr-2">Loading…</span>}
              <button onClick={close} aria-label="Close search" className="text-gray-400 hover:text-gray-600 p-1">
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            {/* Results */}
            {q ? (
              <div className="max-h-80 overflow-y-auto">
                {results.length === 0 && !loading && (
                  <p className="text-sm text-gray-400 text-center py-8">No results for &ldquo;{query}&rdquo;</p>
                )}

                {matchedLists.length > 0 && (
                  <section aria-label="Lists">
                    <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider px-4 pt-3 pb-1">Lists</p>
                    {matchedLists.map(l => {
                      const idx = results.findIndex(r => r.type === 'list' && r.id === l.id);
                      return (
                        <button
                          key={l.id}
                          onClick={() => go({ type: 'list', id: l.id, title: l.name })}
                          className={`w-full text-left px-4 py-2.5 text-sm flex items-center gap-3 transition-colors ${activeIndex === idx ? 'bg-indigo-50 text-indigo-700' : 'text-gray-700 hover:bg-gray-50'}`}
                        >
                          <svg className="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                          </svg>
                          {l.name}
                        </button>
                      );
                    })}
                  </section>
                )}

                {matchedRecipes.length > 0 && (
                  <section aria-label="Recipes">
                    <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider px-4 pt-3 pb-1">Recipes</p>
                    {matchedRecipes.map(r => {
                      const idx = results.findIndex(res => res.type === 'recipe' && res.id === r.id);
                      return (
                        <button
                          key={r.id}
                          onClick={() => go({ type: 'recipe', id: r.id, title: r.title })}
                          className={`w-full text-left px-4 py-2.5 text-sm flex items-center gap-3 transition-colors ${activeIndex === idx ? 'bg-indigo-50 text-indigo-700' : 'text-gray-700 hover:bg-gray-50'}`}
                        >
                          <svg className="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                          </svg>
                          {r.title}
                        </button>
                      );
                    })}
                  </section>
                )}
              </div>
            ) : (
              !loading && (
                <p className="text-sm text-gray-400 text-center py-8">Type to search your lists and recipes</p>
              )
            )}
          </div>
        </div>
      )}
    </>
  );
};

export default GlobalSearch;
