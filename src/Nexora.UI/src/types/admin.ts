/**
 * Admin API Types
 */

export interface Synonym {
  term: string;
  synonyms: string[];
  isActive: boolean;
  createdAt: string;
}

export interface SynonymRequest {
  term: string;
  synonyms: string[];
}

export interface ReindexJob {
  jobId?: string;
  status: 'accepted' | 'pending' | 'running' | 'completed' | 'failed';
  message?: string;
  startedAt?: string;
  completedAt?: string;
  documentsProcessed?: number;
  totalDocuments?: number;
  errorMessage?: string;
}

export interface RankingConfig {
  textScoreWeight: number;
  availabilityWeight: number;
  ratingWeight: number;
  popularityWeight: number;
  lastUpdatedAt?: string;
  lastUpdatedBy?: string;
}

export interface RankingConfigRequest {
  textScoreWeight: number;
  availabilityWeight: number;
  ratingWeight: number;
  popularityWeight: number;
}
