/**
 * Admin API Types
 */

// Synonyms
export interface Synonym {
  id: string;
  terms: string[];
  createdAt: string;
  updatedAt: string;
}

export interface SynonymRequest {
  terms: string[];
}

export interface SynonymsResponse {
  synonyms: Synonym[];
  totalCount: number;
}

// Reindex
export interface ReindexJob {
  jobId: string;
  status: 'pending' | 'running' | 'completed' | 'failed';
  startedAt: string;
  completedAt?: string;
  documentsProcessed: number;
  totalDocuments: number;
  errorMessage?: string;
}

export interface ReindexRequest {
  triggerFullReindex?: boolean;
}

export interface ReindexResponse {
  jobId: string;
  status: string;
}

// Ranking Configuration
export interface RankingConfig {
  textScoreWeight: number;
  ctrWeight: number;
  conversionWeight: number;
  recencyWeight: number;
  popularityWeight: number;
  version: number;
  lastUpdatedAt: string;
  lastUpdatedBy: string;
}

export interface RankingConfigRequest {
  textScoreWeight: number;
  ctrWeight: number;
  conversionWeight: number;
  recencyWeight: number;
  popularityWeight: number;
}
