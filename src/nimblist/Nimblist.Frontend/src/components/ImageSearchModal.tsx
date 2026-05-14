import React, { useState, useEffect, useRef } from 'react';
import { authenticatedFetch } from './HttpHelper';

interface ImageResult {
  title: string | null;
  imageUrl: string;
  thumbnailUrl: string | null;
  sourceUrl: string | null;
}

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (url: string) => void;
  initialQuery: string;
}

const ImageSearchModal: React.FC<Props> = ({ isOpen, onClose, onSelect, initialQuery }) => {
  const [query, setQuery] = useState(initialQuery);
  const [results, setResults] = useState<ImageResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Re-sync query and auto-search whenever the modal opens
  useEffect(() => {
    if (isOpen) {
      setQuery(initialQuery);
      setResults([]);
      setError(null);
      setHasSearched(false);
      if (initialQuery.trim()) {
        runSearch(initialQuery);
      }
      // Focus the search input after a short paint delay
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const runSearch = async (q: string) => {
    if (!q.trim()) return;
    setIsLoading(true);
    setError(null);
    setResults([]);
    setHasSearched(true);
    try {
      const response = await authenticatedFetch(`/api/imagesearch?q=${encodeURIComponent(q.trim())}`);
      if (response.ok) {
        const data: ImageResult[] = await response.json();
        setResults(data);
      } else {
        const body = await response.json().catch(() => null);
        setError(body?.error ?? `Search failed (${response.status}).`);
      }
    } catch {
      setError('Network error — could not perform image search.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    runSearch(query);
  };

  const handleSelect = (url: string) => {
    onSelect(url);
    onClose();
  };

  // Close on Escape key
  useEffect(() => {
    if (!isOpen) return;
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      onClick={e => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] flex flex-col mx-4">

        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 className="font-semibold text-gray-800">Find Recipe Image</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Search bar */}
        <div className="px-5 py-3 border-b border-gray-100">
          <form onSubmit={handleSubmit} className="flex gap-2">
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={e => setQuery(e.target.value)}
              placeholder="Search for images…"
              className="flex-grow px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
            />
            <button
              type="submit"
              disabled={isLoading || !query.trim()}
              className="px-4 py-2 bg-indigo-600 text-white text-sm font-semibold rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isLoading ? 'Searching…' : 'Search'}
            </button>
          </form>
        </div>

        {/* Results area */}
        <div className="flex-1 overflow-y-auto p-5">
          {error && (
            <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded p-3">{error}</p>
          )}

          {isLoading && (
            <div className="flex items-center justify-center py-12">
              <p className="text-sm text-gray-500">Searching…</p>
            </div>
          )}

          {!isLoading && !error && !hasSearched && (
            <p className="text-sm text-gray-400 text-center py-12">
              Results will appear here
            </p>
          )}

          {!isLoading && !error && hasSearched && results.length === 0 && (
            <p className="text-sm text-gray-500 text-center py-12">No images found. Try a different query.</p>
          )}

          {!isLoading && results.length > 0 && (
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {results.map((result, i) => (
                <button
                  key={i}
                  type="button"
                  onClick={() => handleSelect(result.imageUrl)}
                  title={result.title ?? undefined}
                  className="group relative aspect-square overflow-hidden rounded-lg border-2 border-transparent hover:border-indigo-400 focus:outline-none focus:border-indigo-500 transition-all bg-gray-100"
                >
                  <img
                    src={result.thumbnailUrl ?? result.imageUrl}
                    alt={result.title ?? ''}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-200"
                    onError={e => {
                      // Fall back to full image if thumbnail fails
                      const img = e.target as HTMLImageElement;
                      if (img.src !== result.imageUrl) {
                        img.src = result.imageUrl;
                      } else {
                        img.style.display = 'none';
                      }
                    }}
                  />
                  {/* Hover overlay */}
                  <div className="absolute inset-0 bg-indigo-600/0 group-hover:bg-indigo-600/10 transition-colors pointer-events-none" />
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Footer hint */}
        <div className="px-5 py-3 border-t border-gray-100 text-xs text-gray-400">
          Click an image to use it as the recipe image. Powered by Bing Image Search.
        </div>
      </div>
    </div>
  );
};

export default ImageSearchModal;
