import React, { useEffect, useState } from 'react';
import { authenticatedFetch } from '../components/HttpHelper';

const PROVIDERS = ['openrouter', 'openai', 'anthropic', 'gemini', 'ollama'] as const;
type Provider = typeof PROVIDERS[number];

interface LlmSettings {
  provider: Provider | '';
  model: string;
  visionModel: string;
  apiKey: string;
  baseUrl: string;
  imageSearchApiKey: string;
  updatedAt?: string;
}

interface AdminUser {
  userId: string;
  email: string;
  roles: string[];
  isComplimentaryAccess: boolean;
}

interface AdminFamilyMember {
  memberId: string;
  userId: string;
  email: string;
  role: string;
  joinedAt: string;
}

interface AdminFamily {
  id: string;
  name: string;
  ownerUserId: string;
  ownerEmail: string;
  members: AdminFamilyMember[];
}

interface AdminFeedback {
  id: string;
  itemName: string;
  categoryName: string | null;
  subCategoryName: string | null;
  userEmail: string | null;
  createdAt: string;
}

type Tab = 'users' | 'families' | 'llm' | 'feedback';

// ── Sub-components (keep tab complexity out of the main component) ──────────

interface UsersTabProps {
  users: AdminUser[];
  loading: boolean;
  error: string | null;
  roleChangeLoading: string | null;
  complimentaryChangeLoading: string | null;
  onRefresh: () => void;
  onSetRole: (userId: string, role: string) => void;
  onSetComplimentaryAccess: (userId: string, value: boolean) => void;
  onDelete: (userId: string, email: string) => void;
}

const UsersTab: React.FC<UsersTabProps> = ({ users, loading, error, roleChangeLoading, complimentaryChangeLoading, onRefresh, onSetRole, onSetComplimentaryAccess, onDelete }) => (
  <div>
    <div className="flex justify-between items-center mb-4">
      <h2 className="text-lg font-semibold text-gray-800">Application Users</h2>
      <button onClick={onRefresh} className="text-sm text-indigo-600 hover:underline">Refresh</button>
    </div>
    {loading && <p className="text-gray-500">Loading...</p>}
    {error && <p className="text-red-600">{error}</p>}
    {!loading && !error && (
      <div className="overflow-x-auto rounded-md border border-gray-200">
        <table className="min-w-full divide-y divide-gray-200 bg-white">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Role</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Free Premium</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map(user => {
              const currentRole = user.roles[0] ?? 'Standard';
              const isChanging = roleChangeLoading === user.userId;
              const isComplimentaryChanging = complimentaryChangeLoading === user.userId;
              const isAdmin = currentRole === 'Admin';
              return (
                <tr key={user.userId}>
                  <td className="px-4 py-2 text-sm text-gray-900">{user.email}</td>
                  <td className="px-4 py-2">
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-semibold ${
                      isAdmin ? 'bg-indigo-100 text-indigo-700' : 'bg-gray-100 text-gray-600'
                    }`}>
                      {currentRole}
                    </span>
                  </td>
                  <td className="px-4 py-2">
                    {isAdmin ? (
                      <span className="text-xs text-gray-400 italic">included (Admin)</span>
                    ) : (
                      <button
                        onClick={() => onSetComplimentaryAccess(user.userId, !user.isComplimentaryAccess)}
                        disabled={isComplimentaryChanging}
                        title={user.isComplimentaryAccess ? 'Revoke free premium access' : 'Grant free premium access'}
                        className={`text-xs px-2 py-1 rounded disabled:opacity-50 transition-colors ${
                          user.isComplimentaryAccess
                            ? 'bg-green-600 text-white hover:bg-green-700'
                            : 'bg-gray-200 text-gray-600 hover:bg-gray-300'
                        }`}
                      >
                        {isComplimentaryChanging ? '…' : user.isComplimentaryAccess ? 'Active' : 'Grant'}
                      </button>
                    )}
                  </td>
                  <td className="px-4 py-2 flex gap-2">
                    {!isAdmin && (
                      <button
                        onClick={() => onSetRole(user.userId, 'Admin')}
                        disabled={isChanging}
                        className="text-xs px-2 py-1 bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50"
                      >
                        Make Admin
                      </button>
                    )}
                    {isAdmin && (
                      <button
                        onClick={() => onSetRole(user.userId, 'Standard')}
                        disabled={isChanging}
                        className="text-xs px-2 py-1 bg-gray-500 text-white rounded hover:bg-gray-600 disabled:opacity-50"
                      >
                        Make Standard
                      </button>
                    )}
                    <button
                      onClick={() => onDelete(user.userId, user.email ?? '')}
                      className="text-xs px-2 py-1 bg-red-600 text-white rounded hover:bg-red-700"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    )}
  </div>
);

interface FamiliesTabProps {
  families: AdminFamily[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  onRemoveMember: (familyId: string, memberId: string) => void;
  onDeleteFamily: (familyId: string, name: string) => void;
}

const FamiliesTab: React.FC<FamiliesTabProps> = ({ families, loading, error, onRefresh, onRemoveMember, onDeleteFamily }) => (
  <div>
    <div className="flex justify-between items-center mb-4">
      <h2 className="text-lg font-semibold text-gray-800">All Families</h2>
      <button onClick={onRefresh} className="text-sm text-indigo-600 hover:underline">Refresh</button>
    </div>
    {loading && <p className="text-gray-500">Loading...</p>}
    {error && <p className="text-red-600">{error}</p>}
    {!loading && !error && families.map(family => (
      <div key={family.id} className="mb-6 border border-gray-200 rounded-md bg-white overflow-hidden">
        <div className="flex justify-between items-center px-4 py-3 bg-gray-50 border-b border-gray-200">
          <div>
            <span className="font-semibold text-gray-800">{family.name}</span>
            <span className="ml-2 text-xs text-gray-500">Owner: {family.ownerEmail}</span>
          </div>
          <button
            onClick={() => onDeleteFamily(family.id, family.name)}
            className="text-xs px-2 py-1 bg-red-600 text-white rounded hover:bg-red-700"
          >
            Delete Family
          </button>
        </div>
        {family.members.length === 0 ? (
          <p className="px-4 py-3 text-sm text-gray-500 italic">No members.</p>
        ) : (
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
                <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Role</th>
                <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Joined</th>
                <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {family.members.map(member => (
                <tr key={member.memberId}>
                  <td className="px-4 py-2 text-sm text-gray-900">{member.email}</td>
                  <td className="px-4 py-2 text-sm text-gray-600">{member.role}</td>
                  <td className="px-4 py-2 text-sm text-gray-500">
                    {new Date(member.joinedAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-2">
                    <button
                      onClick={() => onRemoveMember(family.id, member.memberId)}
                      className="text-xs px-2 py-1 bg-red-600 text-white rounded hover:bg-red-700"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    ))}
  </div>
);

interface FeedbackTabProps {
  feedback: AdminFeedback[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  onDelete: (id: string, itemName: string) => void;
}

const FeedbackTab: React.FC<FeedbackTabProps> = ({ feedback, loading, error, onRefresh, onDelete }) => (
  <div>
    <div className="flex justify-between items-center mb-4">
      <div>
        <h2 className="text-lg font-semibold text-gray-800">Classification Feedback</h2>
        <p className="text-sm text-gray-500 mt-0.5">
          User-corrected item classifications stored for ML retraining.{' '}
          {feedback.length > 0 && `${feedback.length} record${feedback.length !== 1 ? 's' : ''}.`}
        </p>
      </div>
      <button onClick={onRefresh} className="text-sm text-indigo-600 hover:underline">Refresh</button>
    </div>
    {loading && <p className="text-gray-500">Loading...</p>}
    {error && <p className="text-red-600">{error}</p>}
    {!loading && !error && feedback.length === 0 && (
      <p className="text-sm text-gray-500 italic">No feedback records.</p>
    )}
    {!loading && !error && feedback.length > 0 && (
      <div className="overflow-x-auto rounded-md border border-gray-200">
        <table className="min-w-full divide-y divide-gray-200 bg-white">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Item Name</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Category</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Subcategory</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">User</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Recorded</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {feedback.map(f => (
              <tr key={f.id}>
                <td className="px-4 py-2 text-sm font-medium text-gray-900">{f.itemName}</td>
                <td className="px-4 py-2 text-sm text-gray-600">{f.categoryName ?? <span className="italic text-gray-400">None</span>}</td>
                <td className="px-4 py-2 text-sm text-gray-600">{f.subCategoryName ?? <span className="italic text-gray-400">None</span>}</td>
                <td className="px-4 py-2 text-sm text-gray-500">{f.userEmail ?? '—'}</td>
                <td className="px-4 py-2 text-sm text-gray-500 whitespace-nowrap">
                  {new Date(f.createdAt).toLocaleDateString()}
                </td>
                <td className="px-4 py-2">
                  <button
                    onClick={() => onDelete(f.id, f.itemName)}
                    className="text-xs px-2 py-1 bg-red-600 text-white rounded hover:bg-red-700"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )}
  </div>
);

const MODEL_PLACEHOLDERS: Record<Provider | '', string> = {
  openai: 'gpt-4o-mini',
  anthropic: 'claude-3-5-haiku-20241022',
  gemini: 'gemini-2.0-flash',
  openrouter: 'anthropic/claude-3-haiku',
  ollama: 'llama3.2',
  '': 'model name',
};

const VISION_PLACEHOLDERS: Record<Provider | '', string> = {
  openai: 'gpt-4o',
  anthropic: 'claude-3-5-sonnet-20241022',
  gemini: 'gemini-2.0-flash',
  openrouter: 'optional vision-capable model',
  ollama: 'optional vision-capable model',
  '': 'optional vision-capable model',
};

interface LlmTabProps {
  llm: LlmSettings;
  loading: boolean;
  saving: boolean;
  error: string | null;
  success: boolean;
  onRefresh: () => void;
  onSave: (e: React.FormEvent) => void;
  onChange: React.Dispatch<React.SetStateAction<LlmSettings>>;
}

const LlmTab: React.FC<LlmTabProps> = ({ llm, loading, saving, error, success, onRefresh, onSave, onChange }) => (
  <div className="max-w-lg">
    <div className="flex justify-between items-center mb-4">
      <h2 className="text-lg font-semibold text-gray-800">LLM Settings</h2>
      <button onClick={onRefresh} className="text-sm text-indigo-600 hover:underline">Refresh</button>
    </div>
    <p className="text-sm text-gray-500 mb-4">
      Configure the LLM provider used for recipe scraping fallback and image import.
      Settings are applied immediately — no restart required.
    </p>
    {loading && <p className="text-gray-500">Loading...</p>}
    {!loading && (
      <form onSubmit={onSave} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Provider</label>
          <select
            className="w-full border rounded px-3 py-2 text-sm"
            value={llm.provider}
            onChange={e => onChange(prev => ({ ...prev, provider: e.target.value as Provider | '' }))}
          >
            <option value="">— disabled —</option>
            {PROVIDERS.map(p => <option key={p} value={p}>{p}</option>)}
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Text model</label>
          <input
            type="text"
            className="w-full border rounded px-3 py-2 text-sm font-mono"
            value={llm.model}
            onChange={e => onChange(prev => ({ ...prev, model: e.target.value }))}
            placeholder={MODEL_PLACEHOLDERS[llm.provider]}
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Vision model <span className="text-gray-400 font-normal">(optional — falls back to text model)</span>
          </label>
          <input
            type="text"
            className="w-full border rounded px-3 py-2 text-sm font-mono"
            value={llm.visionModel}
            onChange={e => onChange(prev => ({ ...prev, visionModel: e.target.value }))}
            placeholder={VISION_PLACEHOLDERS[llm.provider]}
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            API key{' '}
            {llm.provider === 'ollama' && <span className="text-gray-400 font-normal">(not required for Ollama)</span>}
          </label>
          <input
            type="password"
            className="w-full border rounded px-3 py-2 text-sm font-mono"
            value={llm.apiKey}
            onChange={e => onChange(prev => ({ ...prev, apiKey: e.target.value }))}
            placeholder={llm.apiKey ? 'Leave blank to keep existing key' : 'Paste API key'}
            autoComplete="off"
          />
          {llm.apiKey && !llm.apiKey.includes('****') && (
            <p className="text-xs text-amber-600 mt-1">New key will be saved on submit.</p>
          )}
        </div>

        {llm.provider === 'ollama' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Ollama base URL</label>
            <input
              type="text"
              className="w-full border rounded px-3 py-2 text-sm font-mono"
              value={llm.baseUrl}
              onChange={e => onChange(prev => ({ ...prev, baseUrl: e.target.value }))}
              placeholder="http://localhost:11434"
            />
          </div>
        )}

        {/* ── Image Search ── */}
        <div className="pt-4 border-t border-gray-200">
          <h3 className="text-sm font-medium text-gray-700 mb-1">Image Search</h3>
          <p className="text-xs text-gray-400 mb-3">
            Used for the "Find image" feature when editing recipes. Requires a free{' '}
            <a href="https://api.search.brave.com/" target="_blank" rel="noopener noreferrer"
              className="text-indigo-600 hover:underline">
              Brave Search API
            </a>{' '}
            key (free tier: 2,000 calls/month). Sign up, create a subscription under
            "Data for Search", and copy the API key.
          </p>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Brave Search API key</label>
            <input
              type="password"
              className="w-full border rounded px-3 py-2 text-sm font-mono"
              value={llm.imageSearchApiKey}
              onChange={e => onChange(prev => ({ ...prev, imageSearchApiKey: e.target.value }))}
              placeholder={llm.imageSearchApiKey ? 'Leave blank to keep existing key' : 'Paste Brave Search API key'}
              autoComplete="off"
            />
            {llm.imageSearchApiKey && !llm.imageSearchApiKey.includes('****') && (
              <p className="text-xs text-amber-600 mt-1">New key will be saved on submit.</p>
            )}
          </div>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}
        {success && <p className="text-sm text-green-600">Settings saved.</p>}

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={saving}
            className="px-4 py-2 bg-indigo-600 text-white text-sm rounded hover:bg-indigo-700 disabled:opacity-50"
          >
            {saving ? 'Saving…' : 'Save settings'}
          </button>
          {llm.updatedAt && (
            <span className="text-xs text-gray-400">
              Last updated {new Date(llm.updatedAt).toLocaleString()}
            </span>
          )}
        </div>
      </form>
    )}
  </div>
);

// ── Helper to produce an updated families array with one member removed ──────

function withMemberRemoved(families: AdminFamily[], familyId: string, memberId: string): AdminFamily[] {
  return families.map(f =>
    f.id !== familyId ? f : { ...f, members: f.members.filter(m => m.memberId !== memberId) }
  );
}

// ── Main component ────────────────────────────────────────────────────────────

const AdminPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<Tab>('users');

  const [users, setUsers] = useState<AdminUser[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [roleChangeLoading, setRoleChangeLoading] = useState<string | null>(null);
  const [complimentaryChangeLoading, setComplimentaryChangeLoading] = useState<string | null>(null);

  const [families, setFamilies] = useState<AdminFamily[]>([]);
  const [familiesLoading, setFamiliesLoading] = useState(false);
  const [familiesError, setFamiliesError] = useState<string | null>(null);

  const [feedback, setFeedback] = useState<AdminFeedback[]>([]);
  const [feedbackLoading, setFeedbackLoading] = useState(false);
  const [feedbackError, setFeedbackError] = useState<string | null>(null);

  const emptyLlm: LlmSettings = { provider: '', model: '', visionModel: '', apiKey: '', baseUrl: '', imageSearchApiKey: '' };
  const [llm, setLlm] = useState<LlmSettings>(emptyLlm);
  const [llmLoading, setLlmLoading] = useState(false);
  const [llmSaving, setLlmSaving] = useState(false);
  const [llmError, setLlmError] = useState<string | null>(null);
  const [llmSuccess, setLlmSuccess] = useState(false);

  const loaders: Record<Tab, () => void> = {
    users: loadUsers, families: loadFamilies, llm: loadLlmSettings, feedback: loadFeedback,
  };

  useEffect(() => { loaders[activeTab]?.(); }, [activeTab]);

  async function loadUsers() {
    setUsersLoading(true); setUsersError(null);
    try {
      const res = await authenticatedFetch('/api/admin/users');
      if (!res.ok) throw new Error(`Failed to load users (${res.status})`);
      setUsers(await res.json());
    } catch (e) {
      setUsersError(e instanceof Error ? e.message : 'Failed to load users');
    } finally { setUsersLoading(false); }
  }

  async function loadFamilies() {
    setFamiliesLoading(true); setFamiliesError(null);
    try {
      const res = await authenticatedFetch('/api/admin/families');
      if (!res.ok) throw new Error(`Failed to load families (${res.status})`);
      setFamilies(await res.json());
    } catch (e) {
      setFamiliesError(e instanceof Error ? e.message : 'Failed to load families');
    } finally { setFamiliesLoading(false); }
  }

  async function loadFeedback() {
    setFeedbackLoading(true); setFeedbackError(null);
    try {
      const res = await authenticatedFetch('/api/admin/classification-feedback');
      if (!res.ok) throw new Error(`Failed to load feedback (${res.status})`);
      setFeedback(await res.json());
    } catch (e) {
      setFeedbackError(e instanceof Error ? e.message : 'Failed to load feedback');
    } finally { setFeedbackLoading(false); }
  }

  async function loadLlmSettings() {
    setLlmLoading(true); setLlmError(null);
    try {
      const res = await authenticatedFetch('/api/admin/llm-settings');
      if (!res.ok) throw new Error(`Failed to load LLM settings (${res.status})`);
      const data = await res.json();
      setLlm({
        provider: data.provider ?? '',
        model: data.model ?? '',
        visionModel: data.visionModel ?? '',
        apiKey: data.apiKey ?? '',
        baseUrl: data.baseUrl ?? '',
        imageSearchApiKey: data.imageSearchApiKey ?? '',
        updatedAt: data.updatedAt,
      });
    } catch (e) {
      setLlmError(e instanceof Error ? e.message : 'Failed to load LLM settings');
    } finally { setLlmLoading(false); }
  }

  async function handleDeleteFeedback(id: string, itemName: string) {
    if (!window.confirm(`Delete feedback record for "${itemName}"?`)) return;
    try {
      const res = await authenticatedFetch(`/api/admin/classification-feedback/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to delete record (${res.status})`);
      setFeedback(prev => prev.filter(f => f.id !== id));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to delete record');
    }
  }

  async function handleSetComplimentaryAccess(userId: string, value: boolean) {
    setComplimentaryChangeLoading(userId);
    try {
      const res = await authenticatedFetch(`/api/admin/users/${userId}/complimentary-access`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isComplimentaryAccess: value }),
      });
      if (!res.ok) throw new Error(`Failed to update access (${res.status})`);
      setUsers(prev => prev.map(u => u.userId === userId ? { ...u, isComplimentaryAccess: value } : u));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to update access');
    } finally { setComplimentaryChangeLoading(null); }
  }

  async function handleSetRole(userId: string, role: string) {
    setRoleChangeLoading(userId);
    try {
      const res = await authenticatedFetch(`/api/admin/users/${userId}/role`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ role }),
      });
      if (!res.ok) throw new Error(`Failed to update role (${res.status})`);
      setUsers(prev => prev.map(u => u.userId === userId ? { ...u, roles: [role] } : u));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to update role');
    } finally { setRoleChangeLoading(null); }
  }

  async function handleDeleteUser(userId: string, email: string) {
    if (!window.confirm(`Delete user "${email}"? This cannot be undone.`)) return;
    try {
      const res = await authenticatedFetch(`/api/admin/users/${userId}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to delete user (${res.status})`);
      setUsers(prev => prev.filter(u => u.userId !== userId));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to delete user');
    }
  }

  async function handleRemoveMember(familyId: string, memberId: string) {
    if (!window.confirm('Remove this member from the family?')) return;
    try {
      const res = await authenticatedFetch(`/api/admin/families/${familyId}/members/${memberId}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to remove member (${res.status})`);
      setFamilies(prev => withMemberRemoved(prev, familyId, memberId));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to remove member');
    }
  }

  async function handleDeleteFamily(familyId: string, name: string) {
    if (!window.confirm(`Delete family "${name}"? This cannot be undone.`)) return;
    try {
      const res = await authenticatedFetch(`/api/admin/families/${familyId}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to delete family (${res.status})`);
      setFamilies(prev => prev.filter(f => f.id !== familyId));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to delete family');
    }
  }

  const saveLlmSettings = async (e: React.FormEvent) => {
    e.preventDefault();
    setLlmSaving(true); setLlmError(null); setLlmSuccess(false);
    try {
      const res = await authenticatedFetch('/api/admin/llm-settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          provider: llm.provider || null,
          model: llm.model || null,
          visionModel: llm.visionModel || null,
          apiKey: llm.apiKey || null,
          baseUrl: llm.baseUrl || null,
          imageSearchApiKey: llm.imageSearchApiKey || null,
        }),
      });
      if (!res.ok) throw new Error(`Failed to save LLM settings (${res.status})`);
      const data = await res.json();
      setLlm(prev => ({ ...prev, apiKey: data.apiKey ?? '', imageSearchApiKey: data.imageSearchApiKey ?? '', updatedAt: data.updatedAt }));
      setLlmSuccess(true);
      setTimeout(() => setLlmSuccess(false), 3000);
    } catch (e) {
      setLlmError(e instanceof Error ? e.message : 'Failed to save LLM settings');
    } finally { setLlmSaving(false); }
  };

  const tabClass = (tab: Tab) =>
    `px-4 py-2 text-sm font-medium rounded-t-md border-b-2 transition-colors ${
      activeTab === tab
        ? 'border-indigo-600 text-indigo-600 bg-white'
        : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
    }`;

  return (
    <div className="max-w-5xl mx-auto">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Admin Panel</h1>

      <div className="border-b border-gray-200 mb-6 flex gap-1">
        <button className={tabClass('users')} onClick={() => setActiveTab('users')}>Users</button>
        <button className={tabClass('families')} onClick={() => setActiveTab('families')}>Families</button>
        <button className={tabClass('llm')} onClick={() => setActiveTab('llm')}>LLM Settings</button>
        <button className={tabClass('feedback')} onClick={() => setActiveTab('feedback')}>Classification Feedback</button>
      </div>

      {activeTab === 'users' && (
        <UsersTab
          users={users} loading={usersLoading} error={usersError}
          roleChangeLoading={roleChangeLoading}
          complimentaryChangeLoading={complimentaryChangeLoading}
          onRefresh={loadUsers} onSetRole={handleSetRole}
          onSetComplimentaryAccess={handleSetComplimentaryAccess}
          onDelete={handleDeleteUser}
        />
      )}
      {activeTab === 'families' && (
        <FamiliesTab
          families={families} loading={familiesLoading} error={familiesError}
          onRefresh={loadFamilies} onRemoveMember={handleRemoveMember} onDeleteFamily={handleDeleteFamily}
        />
      )}
      {activeTab === 'feedback' && (
        <FeedbackTab
          feedback={feedback} loading={feedbackLoading} error={feedbackError}
          onRefresh={loadFeedback} onDelete={handleDeleteFeedback}
        />
      )}
      {activeTab === 'llm' && (
        <LlmTab
          llm={llm} loading={llmLoading} saving={llmSaving}
          error={llmError} success={llmSuccess}
          onRefresh={loadLlmSettings} onSave={saveLlmSettings} onChange={setLlm}
        />
      )}
    </div>
  );
};

export default AdminPage;
