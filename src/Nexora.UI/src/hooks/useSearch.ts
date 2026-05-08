import { useEffect, useMemo, useState } from "react";
import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { fetchSuggestions, searchProducts, type SearchFilters } from "../services/searchApi";
import { useDebouncedValue } from "./useDebouncedValue";
import type { SearchResponse, SuggestionItem } from "../types/search";

const DEFAULT_PER_PAGE = 12;

export type PriceRange = "all" | "0-50" | "50-100" | "100+";

export interface UseSearchReturn {
  query: string;
  setQuery: (q: string) => void;
  debouncedQuery: string;
  hasSearchInput: boolean;
  page: number;
  setPage: (p: number) => void;
  selectedBrands: string[];
  setSelectedBrands: (brands: string[]) => void;
  selectedCategories: string[];
  setSelectedCategories: (cats: string[]) => void;
  priceRange: PriceRange;
  setPriceRange: (range: PriceRange) => void;
  toggleBrand: (brand: string) => void;
  toggleCategory: (category: string) => void;
  searchResult: UseQueryResult<SearchResponse, Error>;
  suggestResult: UseQueryResult<SuggestionItem[], Error>;
}

export function useSearch(): UseSearchReturn {
  const [query, setQuery] = useState("shoes");
  const [page, setPage] = useState(1);
  const [selectedBrands, setSelectedBrands] = useState<string[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [priceRange, setPriceRange] = useState<PriceRange>("all");

  const debouncedQuery = useDebouncedValue(query, 300);
  const hasSearchInput = debouncedQuery.trim().length >= 2;

  const filters = useMemo<SearchFilters>(
    () => ({
      brands: selectedBrands,
      categories: selectedCategories,
      priceRange,
    }),
    [priceRange, selectedBrands, selectedCategories],
  );

  const searchResult = useQuery<SearchResponse, Error>({
    queryKey: ["search", debouncedQuery, page, filters],
    queryFn: ({ signal }) =>
      searchProducts({
        query: debouncedQuery,
        page,
        perPage: DEFAULT_PER_PAGE,
        filters,
        signal,
      }),
    enabled: hasSearchInput,
  });

  const suggestResult = useQuery<SuggestionItem[], Error>({
    queryKey: ["suggest", debouncedQuery],
    queryFn: ({ signal }) =>
      fetchSuggestions({
        query: debouncedQuery,
        limit: 8,
        signal,
      }),
    enabled: hasSearchInput,
  });

  useEffect(() => {
    setPage(1);
  }, [debouncedQuery, filters]);

  const toggleBrand = (brand: string) => {
    setSelectedBrands((prev) =>
      prev.includes(brand) ? prev.filter((b) => b !== brand) : [...prev, brand],
    );
  };

  const toggleCategory = (category: string) => {
    setSelectedCategories((prev) =>
      prev.includes(category) ? prev.filter((c) => c !== category) : [...prev, category],
    );
  };

  return {
    query,
    setQuery,
    debouncedQuery,
    hasSearchInput,
    page,
    setPage,
    selectedBrands,
    setSelectedBrands,
    selectedCategories,
    setSelectedCategories,
    priceRange,
    setPriceRange,
    toggleBrand,
    toggleCategory,
    searchResult,
    suggestResult,
  };
}

