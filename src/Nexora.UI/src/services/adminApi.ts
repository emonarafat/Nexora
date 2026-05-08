/**
 * Admin API Services
 */

import type {
  Synonym,
  SynonymRequest,
  ReindexJob,
  RankingConfig,
  RankingConfigRequest,
} from '../types/admin';
import { loadToken } from '../features/auth/services/authService';

const ADMIN_API_BASE_URL =
  (import.meta.env.VITE_ADMIN_API_BASE_URL as string | undefined) ?? 'http://localhost:5001';

function authHeaders(): Record<string, string> {
  const token = loadToken();
  if (!token) return {};
  return { Authorization: `Bearer ${token.accessToken}` };
}

// ============================================================================
// SYNONYMS
// ============================================================================

export async function fetchSynonyms(): Promise<Synonym[]> {
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/admin/synonyms/`, {
    headers: authHeaders(),
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<Synonym[]>;
}

export async function createSynonym(term: string, synonyms: string[]): Promise<void> {
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/admin/synonyms/`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify({ term, synonyms } satisfies SynonymRequest),
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
}

export async function deleteSynonym(term: string): Promise<void> {
  const response = await fetch(
    `${ADMIN_API_BASE_URL}/api/v1/admin/synonyms/${encodeURIComponent(term)}`,
    {
      method: 'DELETE',
      headers: authHeaders(),
    },
  );

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
}

// ============================================================================
// REINDEX
// ============================================================================

export async function triggerReindex(): Promise<ReindexJob> {
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/admin/reindex`, {
    method: 'POST',
    headers: authHeaders(),
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);

  const data = (await response.json()) as {
    status?: string;
    Status?: string;
    message?: string;
    Message?: string;
    jobId?: string;
  };

  return {
    jobId: data.jobId,
    status: (data.status ?? data.Status ?? 'accepted').toLowerCase() as ReindexJob['status'],
    message: data.message ?? data.Message,
  };
}

export async function getReindexStatus(jobId?: string): Promise<ReindexJob> {
  const suffix = jobId ? `?jobId=${encodeURIComponent(jobId)}` : '';
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/admin/reindex/status${suffix}`, {
    headers: authHeaders(),
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<ReindexJob>;
}

// ============================================================================
// RANKING CONFIGURATION
// ============================================================================

export async function fetchRankingConfig(): Promise<RankingConfig> {
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/admin/ranking-config`, {
    headers: authHeaders(),
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<RankingConfig>;
}

export async function updateRankingConfig(config: RankingConfigRequest): Promise<RankingConfig> {
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/admin/ranking-config`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(config),
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<RankingConfig>;
}
