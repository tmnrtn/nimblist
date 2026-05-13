import React, { useEffect, useState } from 'react';
import { authenticatedFetch } from '../components/HttpHelper';

interface AdminUser {
  userId: string;
  email: string;
  roles: string[];
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

type Tab = 'users' | 'families';

const AdminPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<Tab>('users');

  // Users state
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [roleChangeLoading, setRoleChangeLoading] = useState<string | null>(null);

  // Families state
  const [families, setFamilies] = useState<AdminFamily[]>([]);
  const [familiesLoading, setFamiliesLoading] = useState(false);
  const [familiesError, setFamiliesError] = useState<string | null>(null);

  useEffect(() => {
    if (activeTab === 'users') loadUsers();
    if (activeTab === 'families') loadFamilies();
  }, [activeTab]);

  const loadUsers = async () => {
    setUsersLoading(true);
    setUsersError(null);
    try {
      const res = await authenticatedFetch('/api/admin/users');
      if (!res.ok) throw new Error(`Failed to load users (${res.status})`);
      setUsers(await res.json());
    } catch (e) {
      setUsersError(e instanceof Error ? e.message : 'Failed to load users');
    } finally {
      setUsersLoading(false);
    }
  };

  const loadFamilies = async () => {
    setFamiliesLoading(true);
    setFamiliesError(null);
    try {
      const res = await authenticatedFetch('/api/admin/families');
      if (!res.ok) throw new Error(`Failed to load families (${res.status})`);
      setFamilies(await res.json());
    } catch (e) {
      setFamiliesError(e instanceof Error ? e.message : 'Failed to load families');
    } finally {
      setFamiliesLoading(false);
    }
  };

  const handleSetRole = async (userId: string, role: string) => {
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
    } finally {
      setRoleChangeLoading(null);
    }
  };

  const handleDeleteUser = async (userId: string, email: string) => {
    if (!window.confirm(`Delete user "${email}"? This cannot be undone.`)) return;
    try {
      const res = await authenticatedFetch(`/api/admin/users/${userId}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to delete user (${res.status})`);
      setUsers(prev => prev.filter(u => u.userId !== userId));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to delete user');
    }
  };

  const handleRemoveMember = async (familyId: string, memberId: string) => {
    if (!window.confirm('Remove this member from the family?')) return;
    try {
      const res = await authenticatedFetch(`/api/admin/families/${familyId}/members/${memberId}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to remove member (${res.status})`);
      setFamilies(prev => prev.map(f =>
        f.id === familyId ? { ...f, members: f.members.filter(m => m.memberId !== memberId) } : f
      ));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to remove member');
    }
  };

  const handleDeleteFamily = async (familyId: string, name: string) => {
    if (!window.confirm(`Delete family "${name}"? This cannot be undone.`)) return;
    try {
      const res = await authenticatedFetch(`/api/admin/families/${familyId}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`Failed to delete family (${res.status})`);
      setFamilies(prev => prev.filter(f => f.id !== familyId));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to delete family');
    }
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
      </div>

      {activeTab === 'users' && (
        <div>
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-lg font-semibold text-gray-800">Application Users</h2>
            <button onClick={loadUsers} className="text-sm text-indigo-600 hover:underline">Refresh</button>
          </div>
          {usersLoading && <p className="text-gray-500">Loading...</p>}
          {usersError && <p className="text-red-600">{usersError}</p>}
          {!usersLoading && !usersError && (
            <div className="overflow-x-auto rounded-md border border-gray-200">
              <table className="min-w-full divide-y divide-gray-200 bg-white">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Role</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {users.map(user => {
                    const currentRole = user.roles[0] ?? 'Standard';
                    const isChanging = roleChangeLoading === user.userId;
                    return (
                      <tr key={user.userId}>
                        <td className="px-4 py-2 text-sm text-gray-900">{user.email}</td>
                        <td className="px-4 py-2">
                          <span className={`inline-block px-2 py-0.5 rounded text-xs font-semibold ${
                            currentRole === 'Admin' ? 'bg-indigo-100 text-indigo-700' : 'bg-gray-100 text-gray-600'
                          }`}>
                            {currentRole}
                          </span>
                        </td>
                        <td className="px-4 py-2 flex gap-2">
                          {currentRole !== 'Admin' && (
                            <button
                              onClick={() => handleSetRole(user.userId, 'Admin')}
                              disabled={isChanging}
                              className="text-xs px-2 py-1 bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50"
                            >
                              Make Admin
                            </button>
                          )}
                          {currentRole !== 'Standard' && (
                            <button
                              onClick={() => handleSetRole(user.userId, 'Standard')}
                              disabled={isChanging}
                              className="text-xs px-2 py-1 bg-gray-500 text-white rounded hover:bg-gray-600 disabled:opacity-50"
                            >
                              Make Standard
                            </button>
                          )}
                          <button
                            onClick={() => handleDeleteUser(user.userId, user.email ?? '')}
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
      )}

      {activeTab === 'families' && (
        <div>
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-lg font-semibold text-gray-800">All Families</h2>
            <button onClick={loadFamilies} className="text-sm text-indigo-600 hover:underline">Refresh</button>
          </div>
          {familiesLoading && <p className="text-gray-500">Loading...</p>}
          {familiesError && <p className="text-red-600">{familiesError}</p>}
          {!familiesLoading && !familiesError && families.map(family => (
            <div key={family.id} className="mb-6 border border-gray-200 rounded-md bg-white overflow-hidden">
              <div className="flex justify-between items-center px-4 py-3 bg-gray-50 border-b border-gray-200">
                <div>
                  <span className="font-semibold text-gray-800">{family.name}</span>
                  <span className="ml-2 text-xs text-gray-500">Owner: {family.ownerEmail}</span>
                </div>
                <button
                  onClick={() => handleDeleteFamily(family.id, family.name)}
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
                            onClick={() => handleRemoveMember(family.id, member.memberId)}
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
      )}
    </div>
  );
};

export default AdminPage;
