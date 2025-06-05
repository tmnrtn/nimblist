import React, { useEffect, useState } from "react";
import { authenticatedFetch } from "../components/HttpHelper";

const PreviousItemNamesPage: React.FC = () => {
  const [names, setNames] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const fetchNames = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await authenticatedFetch("/api/PreviousItemNames", {
        method: "GET",
        headers: { Accept: "application/json" },
      });
      if (!res.ok) throw new Error("Failed to fetch previous item names");
      const data = await res.json();
      setNames(Array.isArray(data) ? [...data].sort((a, b) => a.localeCompare(b)) : []);
    } catch (err) {
      setError("Failed to load previous item names.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchNames();
  }, []);

  const handleDelete = async (name: string) => {
    if (!window.confirm(`Delete previous item name: '${name}'?`)) return;
    setDeleting(name);
    setError(null);
    try {
      const res = await authenticatedFetch(`/api/PreviousItemNames/${encodeURIComponent(name)}`, {
        method: "DELETE",
      });
      if (!res.ok) throw new Error("Failed to delete");
      setNames((prev) => prev.filter((n) => n !== name).sort((a, b) => a.localeCompare(b)));
    } catch (err) {
      setError("Failed to delete previous item name.");
    } finally {
      setDeleting(null);
    }
  };

  return (
    <div className="max-w-xl mx-auto p-4">
      <h2 className="text-xl font-bold mb-4">Previous Item Names</h2>
      {error && <div className="text-red-600 mb-2">{error}</div>}
      {loading ? (
        <div>Loading...</div>
      ) : names.length === 0 ? (
        <div className="text-gray-500">No previous item names found.</div>
      ) : (
        <ul className="divide-y divide-gray-200">
          {names.map((name) => (
            <li key={name} className="flex items-center justify-between py-2">
              <span>{name}</span>
              <button
                className="ml-4 px-2 py-1 text-xs bg-red-600 text-white rounded disabled:opacity-50"
                onClick={() => handleDelete(name)}
                disabled={deleting === name}
              >
                {deleting === name ? "Deleting..." : "Delete"}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

export default PreviousItemNamesPage;
