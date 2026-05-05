import React, { useEffect, useState } from 'react';
import { authenticatedFetch } from './HttpHelper';
import { Family, ListShareDetail, MealPlanShareDetail, RecipeShareDetail } from '../types/index';

interface Props {
  endpoint: string;
  postEndpoint: string;
  resourceId: string;
  resourceKey: 'listId' | 'recipeId' | 'mealPlanId';
  isOwner: boolean;
}

type ShareDetail = ListShareDetail | RecipeShareDetail | MealPlanShareDetail;

const SharePanel: React.FC<Props> = ({ endpoint, postEndpoint, resourceId, resourceKey, isOwner }) => {
  const [families, setFamilies] = useState<Family[]>([]);
  const [shares, setShares] = useState<ShareDetail[]>([]);
  const [selectedFamilyId, setSelectedFamilyId] = useState('');
  const [isSharing, setIsSharing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOwner) return;
    Promise.all([
      authenticatedFetch(endpoint).then(r => r.ok ? r.json() : []),
      authenticatedFetch('/api/families').then(r => r.ok ? r.json() : []),
    ]).then(([sharesData, familiesData]) => {
      setShares(sharesData);
      setFamilies(familiesData);
      if (familiesData.length > 0) setSelectedFamilyId(familiesData[0].id);
    }).catch(() => {});
  }, [endpoint, isOwner]);

  const handleShare = async () => {
    if (!selectedFamilyId) return;
    setIsSharing(true);
    setError(null);
    try {
      const body: Record<string, string | undefined> = { [resourceKey]: resourceId, familyIdToShareWith: selectedFamilyId };
      const response = await authenticatedFetch(postEndpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (response.ok) {
        const newShare = await response.json();
        setShares(prev => [...prev, newShare]);
      } else if (response.status === 409) {
        setError('Already shared with this family.');
      } else {
        const body = await response.json().catch(() => null);
        setError(body?.message ?? 'Failed to share.');
      }
    } catch {
      setError('Network error.');
    } finally {
      setIsSharing(false);
    }
  };

  const handleRemove = async (shareId: string) => {
    const prev = shares;
    setShares(s => s.filter(x => x.id !== shareId));
    try {
      await authenticatedFetch(`${postEndpoint}/${shareId}`, { method: 'DELETE' });
    } catch {
      setShares(prev);
    }
  };

  if (!isOwner) {
    return <p className="text-xs text-gray-400 italic">Shared with you — only the owner can manage sharing.</p>;
  }

  const sharedFamilyIds = new Set(shares.map(s => s.sharedWithFamilyId).filter(Boolean));
  const availableFamilies = families.filter(f => !sharedFamilyIds.has(f.id));

  return (
    <div className="space-y-2">
      {shares.length === 0 && <p className="text-xs text-gray-500">Not shared with anyone yet.</p>}
      {shares.map(s => (
        <div key={s.id} className="flex items-center justify-between text-sm bg-gray-50 px-2 py-1 rounded">
          <span className="text-gray-700">
            {s.sharedWithFamilyName ? `👨‍👩‍👧 ${s.sharedWithFamilyName}` : `👤 ${s.sharedWithEmail ?? s.sharedWithUserId}`}
          </span>
          <button
            onClick={() => handleRemove(s.id)}
            className="text-xs text-red-500 hover:text-red-700"
          >
            Remove
          </button>
        </div>
      ))}

      {error && <p className="text-xs text-red-600">{error}</p>}

      {availableFamilies.length > 0 ? (
        <div className="flex gap-2 mt-1">
          <select
            value={selectedFamilyId}
            onChange={e => setSelectedFamilyId(e.target.value)}
            className="flex-grow text-sm px-2 py-1 border border-gray-300 rounded focus:outline-none focus:ring-indigo-500"
            disabled={isSharing}
          >
            {availableFamilies.map(f => (
              <option key={f.id} value={f.id}>{f.name}</option>
            ))}
          </select>
          <button
            onClick={handleShare}
            disabled={isSharing || !selectedFamilyId}
            className="px-3 py-1 text-sm bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            {isSharing ? '…' : 'Share'}
          </button>
        </div>
      ) : families.length === 0 ? (
        <p className="text-xs text-gray-500">Create a family first to share.</p>
      ) : (
        <p className="text-xs text-gray-500">Shared with all your families.</p>
      )}
    </div>
  );
};

export default SharePanel;
