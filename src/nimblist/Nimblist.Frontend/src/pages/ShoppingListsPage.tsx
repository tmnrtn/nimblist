// src/pages/ShoppingListsPage.tsx
import React, { useState, useEffect, FormEvent } from "react";
import { Link } from "react-router-dom";
import useAuthStore from "../store/authStore";
import { ShoppingList } from "../types/index";
import { authenticatedFetch } from "../components/HttpHelper";
import SharePanel from "../components/SharePanel";

const ShoppingListsPage: React.FC = () => {
  const { isAuthenticated, user } = useAuthStore();
  const [expandedShareId, setExpandedShareId] = useState<string | null>(null);

  const [lists, setLists] = useState<ShoppingList[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // New list form
  const [isAdding, setIsAdding] = useState<boolean>(false);
  const [showNew, setShowNew] = useState<boolean>(false);
  const [addError, setAddError] = useState<string | null>(null);
  const [newListName, setNewListName] = useState<string>("");
  const [newListIsTemplate, setNewListIsTemplate] = useState<boolean>(false);

  // Create-from-template modal
  const [fromTemplateId, setFromTemplateId] = useState<string | null>(null);
  const [fromTemplateName, setFromTemplateName] = useState<string>("");
  const [fromTemplateLoading, setFromTemplateLoading] = useState<boolean>(false);
  const [fromTemplateError, setFromTemplateError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated) {
      setError("Please log in to view your shopping lists.");
      return;
    }

    const fetchUserLists = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const response = await authenticatedFetch("/api/shoppinglists", {
          method: "GET",
          headers: { Accept: "application/json" },
        });
        if (response.ok) {
          const data: ShoppingList[] = await response.json();
          setLists(data);
        } else if (response.status === 401) {
          setError("Your session may have expired. Please log out and log back in.");
        } else {
          setError(`Failed to load lists. Server responded with ${response.status}.`);
        }
      } catch {
        setError("Unable to connect to the server. Please check your network connection.");
      } finally {
        setIsLoading(false);
      }
    };

    fetchUserLists();
  }, [isAuthenticated]);

  const handleDeleteList = async (id: string) => {
    if (!confirm("Delete this list and all its items? This cannot be undone.")) return;
    const prev = lists;
    setLists((l) => l.filter((x) => x.id !== id));
    try {
      await authenticatedFetch(`/api/shoppinglists/${id}`, { method: "DELETE" });
    } catch {
      setLists(prev);
    }
  };

  const handleToggleTemplate = async (list: ShoppingList) => {
    const updated = { name: list.name, isTemplate: !list.isTemplate };
    try {
      const response = await authenticatedFetch(`/api/shoppinglists/${list.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(updated),
      });
      if (response.ok) {
        setLists((prev) =>
          prev.map((l) => (l.id === list.id ? { ...l, isTemplate: !list.isTemplate } : l))
        );
      }
    } catch {
      // ignore
    }
  };

  const handleAddList = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsAdding(true);
    setAddError(null);
    try {
      const response = await authenticatedFetch("/api/shoppinglists", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: newListName, isTemplate: newListIsTemplate }),
      });
      if (response.ok) {
        const newList: ShoppingList = await response.json();
        setLists((prev) => [...prev, newList]);
        setNewListName("");
        setNewListIsTemplate(false);
        setShowNew(false);
      } else if (response.status === 400) {
        setAddError("Failed to add item. Please check your input.");
      } else {
        setAddError("Failed to add item. Please try again later.");
      }
    } catch {
      setAddError("Failed to connect to the server to add item.");
    } finally {
      setIsAdding(false);
    }
  };

  const handleCreateFromTemplate = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!fromTemplateId) return;
    setFromTemplateLoading(true);
    setFromTemplateError(null);
    try {
      const response = await authenticatedFetch(
        `/api/shoppinglists/${fromTemplateId}/createfrom`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ name: fromTemplateName }),
        }
      );
      if (response.ok) {
        const newList: ShoppingList = await response.json();
        setLists((prev) => [...prev, newList]);
        setFromTemplateId(null);
        setFromTemplateName("");
      } else {
        setFromTemplateError("Failed to create list from template.");
      }
    } catch {
      setFromTemplateError("Failed to connect to the server.");
    } finally {
      setFromTemplateLoading(false);
    }
  };

  if (isLoading) return <div className="text-center p-6">Loading lists...</div>;
  if (error) {
    return (
      <div className="p-4 my-4 text-sm text-red-700 bg-red-100 rounded-lg border border-red-400" role="alert">
        {error}
      </div>
    );
  }

  const activeLists = lists.filter((l) => !l.isTemplate);
  const templates = lists.filter((l) => l.isTemplate);

  const renderList = (list: ShoppingList) => {
    const isOwner = list.userId === user?.userId;
    const shareOpen = expandedShareId === list.id;
    return (
      <li key={list.id} className="px-4 py-3 sm:px-6 hover:bg-gray-50 transition-colors">
        <div className="flex items-center justify-between gap-2">
          {list.isTemplate ? (
            <span className="text-lg font-medium text-gray-700 truncate" title={list.name}>
              {list.name}
            </span>
          ) : (
            <Link
              to={`/lists/${list.id}`}
              className="text-lg font-medium text-indigo-600 hover:text-indigo-800 hover:underline truncate"
              title={list.name}
            >
              {list.name}
              {!isOwner && <span className="ml-2 text-xs font-normal text-gray-400">(shared)</span>}
            </Link>
          )}
          <div className="flex items-center gap-2 flex-shrink-0">
            {list.isTemplate && isOwner && (
              <button
                onClick={() => { setFromTemplateId(list.id); setFromTemplateName(`${list.name} (copy)`); }}
                className="text-xs text-green-600 hover:text-green-800 border border-green-200 hover:bg-green-50 px-2 py-0.5 rounded transition-colors"
                title="Create list from this template"
              >
                Use template
              </button>
            )}
            {isOwner && (
              <button
                onClick={() => handleToggleTemplate(list)}
                className="text-xs text-gray-500 hover:text-gray-700 border border-gray-200 hover:bg-gray-100 px-2 py-0.5 rounded transition-colors"
                title={list.isTemplate ? "Convert to active list" : "Save as template"}
              >
                {list.isTemplate ? "Untemplate" : "Make template"}
              </button>
            )}
            {!list.isTemplate && (
              <button
                onClick={() => setExpandedShareId(shareOpen ? null : list.id)}
                className="text-xs text-indigo-500 hover:text-indigo-700 border border-indigo-200 hover:bg-indigo-50 px-2 py-0.5 rounded transition-colors"
              >
                {shareOpen ? "Close" : "Share"}
              </button>
            )}
            {isOwner && (
              <button
                onClick={() => handleDeleteList(list.id)}
                className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:bg-red-50 px-2 py-0.5 rounded transition-colors"
                title="Delete"
              >
                Delete
              </button>
            )}
            <span className="text-xs text-gray-400">{new Date(list.createdAt).toLocaleDateString()}</span>
          </div>
        </div>
        {shareOpen && !list.isTemplate && (
          <div className="mt-3 p-3 bg-gray-50 rounded-md border border-gray-200">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Sharing</p>
            <SharePanel
              endpoint={`/api/listshares?listId=${list.id}`}
              postEndpoint="/api/listshares"
              resourceId={list.id}
              resourceKey="listId"
              isOwner={isOwner}
            />
          </div>
        )}
      </li>
    );
  };

  return (
    <div className="space-y-6">
      {/* Create-from-template modal */}
      {fromTemplateId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm mx-4 p-6 space-y-4">
            <h3 className="text-lg font-semibold text-gray-800">Create list from template</h3>
            {fromTemplateError && (
              <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded p-2">{fromTemplateError}</p>
            )}
            <form onSubmit={handleCreateFromTemplate} className="space-y-3">
              <input
                type="text"
                value={fromTemplateName}
                onChange={(e) => setFromTemplateName(e.target.value)}
                placeholder="New list name"
                required
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <div className="flex gap-2 justify-end">
                <button
                  type="button"
                  onClick={() => setFromTemplateId(null)}
                  className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={fromTemplateLoading || !fromTemplateName.trim()}
                  className="px-4 py-2 text-sm bg-green-600 text-white font-semibold rounded-md hover:bg-green-700 disabled:opacity-50"
                >
                  {fromTemplateLoading ? "Creating..." : "Create list"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Active lists */}
      <div className="space-y-4">
        <div className="flex justify-between items-center">
          <h2 className="text-2xl font-semibold">My Shopping Lists</h2>
          <button
            className="bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded transition-colors"
            onClick={() => setShowNew(!showNew)}
          >
            Create New List
          </button>
        </div>

        {showNew && (
          <form
            onSubmit={handleAddList}
            className="p-4 bg-gray-100 rounded-md shadow space-y-3 border border-gray-200"
          >
            <h3 className="text-lg font-medium text-gray-700">Add New List</h3>
            {addError && (
              <p className="text-sm text-red-600 bg-red-100 p-2 rounded border border-red-300">{addError}</p>
            )}
            <div className="flex flex-col sm:flex-row sm:space-x-2 space-y-2 sm:space-y-0">
              <input
                type="text"
                value={newListName}
                onChange={(e) => setNewListName(e.target.value)}
                placeholder="List Name (required)"
                aria-label="New list name"
                required
                className="flex-grow px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm disabled:bg-gray-200"
                disabled={isAdding}
              />
              <button
                type="submit"
                disabled={isAdding || !newListName.trim()}
                className="px-4 py-2 bg-blue-600 text-white font-semibold rounded-md shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isAdding ? "Adding..." : "Add Item"}
              </button>
            </div>
            <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
              <input
                type="checkbox"
                checked={newListIsTemplate}
                onChange={(e) => setNewListIsTemplate(e.target.checked)}
                className="rounded border-gray-300"
              />
              Save as a reusable template
            </label>
          </form>
        )}

        {activeLists.length === 0 ? (
          <p className="text-gray-500 text-sm py-4">No active lists. Create one above.</p>
        ) : (
          <ul className="bg-white shadow overflow-hidden sm:rounded-md divide-y divide-gray-200">
            {activeLists.map(renderList)}
          </ul>
        )}
      </div>

      {/* Templates section */}
      {templates.length > 0 && (
        <div className="space-y-3">
          <h2 className="text-xl font-semibold text-gray-700">Templates</h2>
          <p className="text-sm text-gray-500">Reusable lists you can copy whenever you need them.</p>
          <ul className="bg-white shadow overflow-hidden sm:rounded-md divide-y divide-gray-200">
            {templates.map(renderList)}
          </ul>
        </div>
      )}
    </div>
  );
};

export default ShoppingListsPage;
