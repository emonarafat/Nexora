export interface FacetValue {
  value: string;
  count: number;
}

export interface ProductResult {
  id: string;
  title: string;
  brand: string;
  sku?: string;
  description?: string;
  category: string;
  price: number;
  currency: string;
  rating: number;
  ratingCount: number;
  stockStatus: string;
  isFeatured: boolean;
  finalScore: number;
}

export interface SearchResponse {
  results: ProductResult[];
  totalCount: number;
  page: number;
  perPage: number;
  totalPages: number;
  facets: Record<string, FacetValue[]>;
  correctedQuery?: string;
  latencyMs: number;
  cacheHit: boolean;
}

export interface SuggestionItem {
  text: string;
  category?: string;
  popularityScore: number;
}
