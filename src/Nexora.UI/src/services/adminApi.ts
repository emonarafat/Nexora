/**
 * Admin API Services
 */

import type {
  Synonym,
  SynonymRequest,
  SynonymsResponse,
  ReindexJob,
  ReindexRequest,
  RankingConfig,
  RankingConfigRequest,
} from '../types/admin';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

// ============================================================================
// SYNONYMS
// ============================================================================

export async function fetchSynonyms(): Promise<Synonym[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/synonyms`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data: SynonymsResponse = await response.json();
    return data.synonyms;
  } catch (error) {
    console.error('Failed to fetch synonyms:', error);
    throw error;
  }
}

export async function createSynonym(terms: string[]): Promise<Synonym> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/synonyms`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ terms } as SynonymRequest),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  } catch (error) {
    console.error('Failed to create synonym:', error);
    throw error;
  }
}

export async function updateSynonym(id: string, terms: string[]): Promise<Synonym> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/synonyms/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ terms } as SynonymRequest),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  } catch (error) {
    console.error('Failed to update synonym:', error);
    throw error;
  }
}

export async function deleteSynonym(id: string): Promise<void> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/synonyms/${id}`, {
      method: 'DELETE',
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
  } catch (error) {
    console.error('Failed to delete synonym:', error);
    throw error;
  }
}

// ============================================================================
// REINDEX
// ============================================================================

export async function triggerReindex(fullReindex: boolean = false): Promise<ReindexJob> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/reindex`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ triggerFullReindex: fullReindex } as ReindexRequest),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  } catch (error) {
    console.error('Failed to trigger reindex:', error);
    throw error;
  }
}

export async function getReindexStatus(jobId: string): Promise<ReindexJob> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/reindex/${jobId}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  } catch (error) {
    console.error('Failed to fetch reindex status:', error);
    throw error;
  }
}

// ============================================================================
// RANKING CONFIGURATION
// ============================================================================

export async function fetchRankingConfig(): Promise<RankingConfig> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/ranking/config`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  } catch (error) {
    console.error('Failed to fetch ranking config:', error);
    throw error;
  }
}

export async function updateRankingConfig(config: RankingConfigRequest): Promise<RankingConfig> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/ranking/config`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  } catch (error) {
    console.error('Failed to update ranking config:', error);
    throw error;
  }
}
