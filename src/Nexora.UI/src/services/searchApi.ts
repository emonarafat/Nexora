import type { SearchResponse, SuggestionItem } from "../types/search";

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "http://localhost:5000";

export interface SearchFilters {
  brands: string[];
  categories: string[];
  priceRange: "all" | "0-50" | "50-100" | "100+";
}

interface SearchParams {
  query: string;
  page: number;
  perPage: number;
  filters: SearchFilters;
  signal?: AbortSignal;
}

interface SuggestParams {
  query: string;
  limit?: number;
  signal?: AbortSignal;
}

function toFilterBy(filters: SearchFilters): string | undefined {
  const clauses: string[] = [];

  if (filters.brands.length > 0) {
    const brandList = filters.brands.join(",");
    clauses.push(filters.brands.length === 1 ? `brand:=${brandList}` : `brand:=[${brandList}]`);
  }

  if (filters.categories.length > 0) {
    const categoryList = filters.categories.join(",");
    clauses.push(filters.categories.length === 1 ? `category:=${categoryList}` : `category:=[${categoryList}]`);
  }

  if (filters.priceRange === "0-50") {
    clauses.push("price:<=50");
  } else if (filters.priceRange === "50-100") {
    clauses.push("price:>=50 && price:<=100");
  } else if (filters.priceRange === "100+") {
    clauses.push("price:>=100");
  }

  if (clauses.length === 0) {
    return undefined;
  }

  return clauses.join(" && ");
}

export async function searchProducts(params: SearchParams): Promise<SearchResponse> {
  const url = new URL("/api/v1/search", apiBaseUrl);
  url.searchParams.set("q", params.query);
  url.searchParams.set("page", String(params.page));
  url.searchParams.set("per_page", String(params.perPage));
  url.searchParams.set("facet_by", "brand,category,price");

  const filterBy = toFilterBy(params.filters);
  if (filterBy) {
    url.searchParams.set("filter_by", filterBy);
  }

  const response = await fetch(url, {
    method: "GET",
    signal: params.signal,
  });

  if (!response.ok) {
    throw new Error(`Search request failed with status ${response.status}`);
  }

  return (await response.json()) as SearchResponse;
}

export async function fetchSuggestions(params: SuggestParams): Promise<SuggestionItem[]> {
  const url = new URL("/api/v1/suggest", apiBaseUrl);
  url.searchParams.set("q", params.query);
  url.searchParams.set("limit", String(params.limit ?? 8));

  const response = await fetch(url, {
    method: "GET",
    signal: params.signal,
  });

  if (!response.ok) {
    throw new Error(`Suggest request failed with status ${response.status}`);
  }

  return (await response.json()) as SuggestionItem[];
}

export function getApiBaseUrl(): string {
  return apiBaseUrl;
}
