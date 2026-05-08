'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  fetchSynonyms,
  createSynonym,
  deleteSynonym,
} from '../../services/adminApi';
import type { Synonym } from '../../types/admin';

type AdminActionStatus = 'success' | 'error';

interface SynonymsSectionProps {
  onAction: (action: string, target: string, status: AdminActionStatus) => void;
}

export function SynonymsSection({ onAction }: SynonymsSectionProps) {
  const queryClient = useQueryClient();
  const [newTerms, setNewTerms] = useState('');

  // Fetch synonyms
  const { data: synonyms = [], isLoading, error } = useQuery({
    queryKey: ['synonyms'],
    queryFn: fetchSynonyms,
  });

  // Create mutation
  const createMutation = useMutation({
    mutationFn: ({ term, synonyms }: { term: string; synonyms: string[] }) => createSynonym(term, synonyms),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['synonyms'] });
      setNewTerms('');
      onAction('Create synonym', 'synonyms list', 'success');
    },
    onError: () => {
      onAction('Create synonym', 'synonyms list', 'error');
    },
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: deleteSynonym,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['synonyms'] });
      onAction('Delete synonym', 'synonyms list', 'success');
    },
    onError: () => {
      onAction('Delete synonym', 'synonyms list', 'error');
    },
  });

  const handleCreate = () => {
    if (!newTerms.trim()) return;
    const terms = newTerms
      .split(',')
      .map((t) => t.trim())
      .filter(Boolean);
    if (terms.length === 0) return;

    const [term, ...synonyms] = terms;
    createMutation.mutate({ term, synonyms: synonyms.length > 0 ? synonyms : [term] });
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
        <h3 className="font-semibold text-slate-900">Existing Synonyms ({synonyms.filter((item) => item.isActive).length})</h3>
        {synonyms.filter((item) => item.isActive).length === 0 ? (
          <p className="text-sm text-slate-500">No synonyms configured yet</p>
        ) : (
          <div className="space-y-2">
            {synonyms
              .filter((item) => item.isActive)
              .map((synonym: Synonym) => (
              <div
                key={synonym.term}
                className="flex items-center justify-between rounded-lg border border-slate-200 p-3"
              >
                <div className="flex-1">
                  <div className="text-sm font-medium text-slate-900">
                    {synonym.term} → {synonym.synonyms.join(' • ')}
                  </div>
                  <div className="text-xs text-slate-500">
                    Created {new Date(synonym.createdAt).toLocaleDateString()}
                  </div>
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => deleteMutation.mutate(synonym.term)}
                    disabled={deleteMutation.isPending}
                    className="text-xs text-red-600 hover:text-red-700"
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
