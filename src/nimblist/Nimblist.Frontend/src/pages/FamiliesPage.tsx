import React, { useEffect, useState, FormEvent } from 'react';
import { authenticatedFetch } from '../components/HttpHelper';
import { Family } from '../types/index';
import useAuthStore from '../store/authStore';

const FamiliesPage: React.FC = () => {
  const { user } = useAuthStore();
  const [families, setFamilies] = useState<Family[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [newFamilyName, setNewFamilyName] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // Per-family add-member state
  const [addEmailMap, setAddEmailMap] = useState<Record<string, string>>({});
  const [addErrorMap, setAddErrorMap] = useState<Record<string, string | null>>({});
  const [addingMap, setAddingMap] = useState<Record<string, boolean>>({});

  useEffect(() => {
    authenticatedFetch('/api/families')
      .then(r => r.json())
      .then((data: Family[]) => setFamilies(data))
      .catch(() => setError('Failed to load families.'))
      .finally(() => setIsLoading(false));
  }, []);

  const handleCreateFamily = async (e: FormEvent) => {
    e.preventDefault();
    if (!newFamilyName.trim()) return;
    setIsCreating(true);
    setCreateError(null);
    try {
      const response = await authenticatedFetch('/api/families', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: newFamilyName.trim() }),
      });
      if (response.ok) {
        const newFamily: Family = await response.json();
        setFamilies(prev => [...prev, newFamily].sort((a, b) => a.name.localeCompare(b.name)));
        setNewFamilyName('');
      } else {
        setCreateError('Failed to create family.');
      }
    } catch {
      setCreateError('Network error.');
    } finally {
      setIsCreating(false);
    }
  };

  const handleDeleteFamily = async (familyId: string) => {
    if (!confirm('Delete this family? All shares using this family will also be removed.')) return;
    const prev = families;
    setFamilies(f => f.filter(x => x.id !== familyId));
    try {
      await authenticatedFetch(`/api/families/${familyId}`, { method: 'DELETE' });
    } catch {
      setFamilies(prev);
    }
  };

  const handleAddMember = async (familyId: string) => {
    const email = (addEmailMap[familyId] ?? '').trim();
    if (!email) return;

    setAddingMap(m => ({ ...m, [familyId]: true }));
    setAddErrorMap(m => ({ ...m, [familyId]: null }));

    try {
      // Step 1: look up user by email
      const lookupResponse = await authenticatedFetch(`/api/auth/lookup?email=${encodeURIComponent(email)}`);
      if (lookupResponse.status === 404) {
        setAddErrorMap(m => ({ ...m, [familyId]: 'No account found with that email address.' }));
        return;
      }
      if (!lookupResponse.ok) {
        setAddErrorMap(m => ({ ...m, [familyId]: 'Failed to look up user.' }));
        return;
      }
      const { userId } = await lookupResponse.json();

      // Step 2: add as family member
      const addResponse = await authenticatedFetch('/api/familymembers', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ familyId, userIdToAdd: userId }),
      });

      if (addResponse.status === 409) {
        setAddErrorMap(m => ({ ...m, [familyId]: 'This person is already a member.' }));
        return;
      }
      if (!addResponse.ok) {
        setAddErrorMap(m => ({ ...m, [familyId]: 'Failed to add member.' }));
        return;
      }

      const newMember = await addResponse.json();
      setFamilies(prev => prev.map(f => {
        if (f.id !== familyId) return f;
        return { ...f, members: [...f.members, newMember] };
      }));
      setAddEmailMap(m => ({ ...m, [familyId]: '' }));
    } catch {
      setAddErrorMap(m => ({ ...m, [familyId]: 'Network error.' }));
    } finally {
      setAddingMap(m => ({ ...m, [familyId]: false }));
    }
  };

  const handleRemoveMember = async (familyId: string, memberId: string, memberUserId: string) => {
    const family = families.find(f => f.id === familyId);
    if (family?.ownerId === memberUserId) {
      alert('The family owner cannot be removed.');
      return;
    }
    const prev = families;
    const removeMember = (f: Family) =>
      f.id !== familyId ? f : { ...f, members: f.members.filter(m => m.id !== memberId) };
    setFamilies(prev => prev.map(removeMember));
    try {
      await authenticatedFetch(`/api/familymembers/${memberId}`, { method: 'DELETE' });
    } catch {
      setFamilies(prev);
    }
  };

  if (isLoading) return <p className="text-gray-500">Loading families…</p>;
  if (error) return <p className="text-red-600">{error}</p>;

  return (
    <div className="space-y-6 max-w-2xl">
      <h2 className="text-2xl font-bold text-gray-800">My Families</h2>

      {/* Create family form */}
      <form onSubmit={handleCreateFamily} className="flex gap-2">
        <input
          type="text"
          value={newFamilyName}
          onChange={e => setNewFamilyName(e.target.value)}
          placeholder="New family name…"
          required
          disabled={isCreating}
          className="flex-grow px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
        />
        <button
          type="submit"
          disabled={isCreating || !newFamilyName.trim()}
          className="px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
        >
          {isCreating ? 'Creating…' : 'Create Family'}
        </button>
      </form>
      {createError && <p className="text-sm text-red-600">{createError}</p>}

      {families.length === 0 && (
        <div className="text-center py-10 px-4">
          <div className="text-5xl mb-3">👨‍👩‍👧</div>
          <h3 className="text-lg font-semibold text-gray-700 mb-1">Share with your household</h3>
          <p className="text-sm text-gray-500 max-w-xs mx-auto">
            Create a Family and invite the people you shop for. They'll be able to view and edit shared lists and recipes in real time.
          </p>
        </div>
      )}

      {/* Family cards */}
      {families.map(family => {
        const isOwner = family.ownerId === user?.userId;
        return (
          <div key={family.id} className="bg-white rounded-lg shadow border border-gray-200 p-4 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-gray-800">{family.name}</h3>
              {isOwner && (
                <button
                  onClick={() => handleDeleteFamily(family.id)}
                  className="text-sm text-red-500 hover:text-red-700 border border-red-300 hover:bg-red-50 px-2 py-0.5 rounded transition-colors"
                >
                  Delete Family
                </button>
              )}
            </div>

            {/* Members list */}
            <div>
              <p className="text-sm font-medium text-gray-600 mb-2">Members</p>
              <ul className="space-y-1.5">
                {family.members.map(member => (
                  <li key={member.id} className="flex items-center justify-between text-sm">
                    <span className="text-gray-700">
                      {member.email ?? member.userId}
                      {member.isOwner && <span className="ml-1.5 text-xs text-indigo-600 font-medium">(Owner)</span>}
                      {member.userId === user?.userId && !member.isOwner && <span className="ml-1.5 text-xs text-gray-400">(you)</span>}
                    </span>
                    {isOwner && !member.isOwner && (
                      <button
                        onClick={() => handleRemoveMember(family.id, member.id, member.userId)}
                        className="text-xs text-red-500 hover:text-red-700"
                      >
                        Remove
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </div>

            {/* Add member (owner only) */}
            {isOwner && (
              <div className="space-y-1">
                <p className="text-sm font-medium text-gray-600">Add member by email</p>
                {addErrorMap[family.id] && (
                  <p className="text-xs text-red-600">{addErrorMap[family.id]}</p>
                )}
                <div className="flex gap-2">
                  <input
                    type="email"
                    value={addEmailMap[family.id] ?? ''}
                    onChange={e => setAddEmailMap(m => ({ ...m, [family.id]: e.target.value }))}
                    placeholder="member@example.com"
                    disabled={addingMap[family.id]}
                    className="flex-grow px-3 py-1.5 border border-gray-300 rounded text-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-200"
                  />
                  <button
                    onClick={() => handleAddMember(family.id)}
                    disabled={addingMap[family.id] || !(addEmailMap[family.id] ?? '').trim()}
                    className="px-3 py-1.5 text-sm bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50 transition-colors"
                  >
                    {addingMap[family.id] ? '…' : 'Add'}
                  </button>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default FamiliesPage;
