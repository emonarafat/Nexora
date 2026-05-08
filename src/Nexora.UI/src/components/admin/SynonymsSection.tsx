'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  fetchSynonyms,
  createSynonym,
  updateSynonym,
  deleteSynonym,
} from '../../services/adminApi';
import type { Synonym } from '../../types/admin';

export function SynonymsSection() {
  const queryClient = useQueryClient();
  const [newTerms, setNewTerms] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editTerms, setEditTerms] = useState('');

  // Fetch synonyms
  const { data: synonyms = [], isLoading, error } = useQuery({
    queryKey: ['synonyms'],
    queryFn: fetchSynonyms,
  });

  // Create mutation
  const createMutation = useMutation({
    mutationFn: (terms: string[]) => createSynonym(terms),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['synonyms'] });
      setNewTerms('');
    },
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: ({ id, terms }: { id: string; terms: string[] }) =>
      updateSynonym(id, terms),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['synonyms'] });
      setEditingId(null);
      setEditTerms('');
    },
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: deleteSynonym,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['synonyms'] });
    },
  });

  const handleCreate = () => {
    if (!newTerms.trim()) return;
    const terms = newTerms.split(',').map((t) => t.trim());
    createMutation.mutate(terms);
  };

  const handleUpdate = (id: string) => {
    if (!editTerms.trim()) return;
    const terms = editTerms.split(',').map((t) => t.trim());
    updateMutation.mutate({ id, terms });
  };

  if (isLoading) {
    return (
      <div className="p-6 text-center">
        <p className="text-slate-600">Loading synonyms...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <div className="rounded-lg bg-red-50 p-4">
          <p className="text-sm text-red-800">
            Error loading synonyms. Make sure the backend API is running at http://localhost:5000
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      {/* Add New Synonym */}
      <div className="space-y-3 rounded-lg bg-slate-50 p-4">
        <h3 className="font-semibold text-slate-900">Add New Synonym Group</h3>
        <p className="text-xs text-slate-600">Separate terms with commas (e.g., "running shoe, sneaker, trainer")</p>
        <div className="flex gap-2">
          <label htmlFor="new-synonym-terms" className="sr-only">
            Enter new synonym terms
          </label>
          <input
            id="new-synonym-terms"
            type="text"
            value={newTerms}
            onChange={(e) => setNewTerms(e.target.value)}
            placeholder="Enter comma-separated synonym terms"
            aria-label="Enter comma-separated synonym terms"
            className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
          />
          <button
            onClick={handleCreate}
            disabled={!newTerms.trim() || createMutation.isPending}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:bg-slate-300"
          >
            {createMutation.isPending ? 'Adding...' : 'Add'}
          </button>
        </div>
      </div>

      {/* Synonyms List */}
      <div className="space-y-3">
        <h3 className="font-semibold text-slate-900">Existing Synonyms ({synonyms.length})</h3>
        {synonyms.length === 0 ? (
          <p className="text-sm text-slate-500">No synonyms configured yet</p>
        ) : (
          <div className="space-y-2">
            {synonyms.map((synonym: Synonym) => (
              <div
                key={synonym.id}
                className="flex items-center justify-between rounded-lg border border-slate-200 p-3"
              >
                {editingId === synonym.id ? (
                  <div className="flex flex-1 gap-2">
                    <label htmlFor={`edit-terms-${synonym.id}`} className="sr-only">
                      Edit synonym terms
                    </label>
                    <input
                      id={`edit-terms-${synonym.id}`}
                      type="text"
                      value={editTerms}
                      onChange={(e) => setEditTerms(e.target.value)}
                      placeholder="Edit comma-separated terms"
                      className="flex-1 rounded border border-slate-300 px-2 py-1 text-sm"
                    />
                    <button
                      onClick={() => handleUpdate(synonym.id)}
                      disabled={updateMutation.isPending}
                      className="rounded bg-green-600 px-3 py-1 text-xs text-white hover:bg-green-700"
                    >
                      Save
                    </button>
                    <button
                      onClick={() => setEditingId(null)}
                      className="rounded bg-slate-400 px-3 py-1 text-xs text-white hover:bg-slate-500"
                    >
                      Cancel
                    </button>
                  </div>
                ) : (
                  <>
                    <div className="flex-1">
                      <div className="text-sm font-medium text-slate-900">
                        {synonym.terms.join(' • ')}
                      </div>
                      <div className="text-xs text-slate-500">
                        Updated {new Date(synonym.updatedAt).toLocaleDateString()}
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <button
                        onClick={() => {
                          setEditingId(synonym.id);
                          setEditTerms(synonym.terms.join(', '));
                        }}
                        className="text-xs text-blue-600 hover:text-blue-700"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => deleteMutation.mutate(synonym.id)}
                        disabled={deleteMutation.isPending}
                        className="text-xs text-red-600 hover:text-red-700"
                      >
                        Delete
                      </button>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
