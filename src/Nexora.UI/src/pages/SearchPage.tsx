import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { emitSearchAnalyticsEvent } from "../services/analyticsService";
import { fetchSuggestions, searchProducts } from "../services/searchApi";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import type { FacetValue } from "../types/search";

const DEFAULT_PER_PAGE = 12;

type PriceRange = "all" | "0-50" | "50-100" | "100+";

function getFacetValues(values: FacetValue[] | undefined): FacetValue[] {
  if (!values) {
    return [];
  }

  return values.filter((item) => item.count > 0);
}

export function SearchPage() {
  const [query, setQuery] = useState("shoes");
  const [page, setPage] = useState(1);
  const [selectedBrands, setSelectedBrands] = useState<string[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [priceRange, setPriceRange] = useState<PriceRange>("all");

  const debouncedQuery = useDebouncedValue(query, 300);
  const hasSearchInput = debouncedQuery.trim().length >= 2;

  const filters = useMemo(
    () => ({
      brands: selectedBrands,
      categories: selectedCategories,
      priceRange,
    }),
    [priceRange, selectedBrands, selectedCategories],
  );

  const searchResult = useQuery({
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

  const suggestResult = useQuery({
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

  const searchData = searchResult.data;
  const brandFacet = getFacetValues(searchData?.facets.brand);
  const categoryFacet = getFacetValues(searchData?.facets.category);

  const toggleSelection = (
    value: string,
    selectedValues: string[],
    setSelectedValues: (next: string[]) => void,
  ) => {
    const exists = selectedValues.includes(value);
    if (exists) {
      setSelectedValues(selectedValues.filter((item) => item !== value));
    } else {
      setSelectedValues([...selectedValues, value]);
    }
  };

  return (
    <section className="space-y-4">
      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-6">
        <h2 className="text-xl font-semibold text-slate-900">Search UI</h2>
        <p className="mt-1 text-sm text-slate-600">
          Debounced suggest, facet filtering, paginated results, and analytics hooks.
        </p>

        <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
          <div>
            <label htmlFor="search-input" className="mb-2 block text-sm font-medium text-slate-700">
              Search products
            </label>
            <input
              id="search-input"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 outline-none ring-0 placeholder:text-slate-400 focus:border-slate-500"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Try: running shoes"
            />
            {!hasSearchInput ? (
              <p className="mt-2 text-xs text-slate-500">Type at least 2 characters to search.</p>
            ) : null}

            {hasSearchInput && suggestResult.data && suggestResult.data.length > 0 ? (
              <ul className="mt-2 rounded-md border border-slate-200 bg-slate-50 p-2 text-sm">
                {suggestResult.data.map((item) => (
                  <li key={`${item.text}-${item.category ?? "uncategorized"}`}>
                    <button
                      type="button"
                      onClick={() => setQuery(item.text)}
                      className="w-full rounded px-2 py-1 text-left text-slate-700 hover:bg-white"
                    >
                      {item.text}
                      {item.category ? (
                        <span className="ml-2 text-xs text-slate-500">in {item.category}</span>
                      ) : null}
                    </button>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>

          <div className="min-w-40">
            <label htmlFor="price-range" className="mb-2 block text-sm font-medium text-slate-700">
              Price range
            </label>
            <select
              id="price-range"
              value={priceRange}
              onChange={(event) => setPriceRange(event.target.value as PriceRange)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900"
            >
              <option value="all">All prices</option>
              <option value="0-50">$0 - $50</option>
              <option value="50-100">$50 - $100</option>
              <option value="100+">$100+</option>
            </select>
          </div>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-[260px_1fr]">
        <aside className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-700">Facets</h3>

          <div className="mt-4 space-y-4">
            <div>
              <p className="mb-2 text-sm font-medium text-slate-800">Brand</p>
              <div className="space-y-1">
                {brandFacet.length === 0 ? (
                  <p className="text-xs text-slate-500">No brand facets yet.</p>
                ) : (
                  brandFacet.map((item) => (
                    <label key={item.value} className="flex items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={selectedBrands.includes(item.value)}
                        onChange={() => toggleSelection(item.value, selectedBrands, setSelectedBrands)}
                      />
                      <span>
                        {item.value} ({item.count})
                      </span>
                    </label>
                  ))
                )}
              </div>
            </div>

            <div>
              <p className="mb-2 text-sm font-medium text-slate-800">Category</p>
              <div className="space-y-1">
                {categoryFacet.length === 0 ? (
                  <p className="text-xs text-slate-500">No category facets yet.</p>
                ) : (
                  categoryFacet.map((item) => (
                    <label key={item.value} className="flex items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={selectedCategories.includes(item.value)}
                        onChange={() => toggleSelection(item.value, selectedCategories, setSelectedCategories)}
                      />
                      <span>
                        {item.value} ({item.count})
                      </span>
                    </label>
                  ))
                )}
              </div>
            </div>
          </div>
        </aside>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
          {searchResult.isPending ? (
            <p className="text-sm text-slate-600">Loading search results...</p>
          ) : null}

          {searchResult.isError ? (
            <p className="text-sm text-red-700">Search API unavailable. Please try again.</p>
          ) : null}

          {searchData ? (
            <>
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 pb-3 text-sm text-slate-600">
                <span>
                  {searchData.totalCount} results in {searchData.latencyMs.toFixed(1)}ms
                  {searchData.cacheHit ? " (cache hit)" : ""}
                </span>
                <span>
                  Page {searchData.page} of {Math.max(searchData.totalPages, 1)}
                </span>
              </div>

              {searchData.totalCount === 0 ? (
                <div className="rounded-md border border-amber-200 bg-amber-50 p-4">
                  <p className="text-sm font-medium text-amber-900">No results found</p>
                  <p className="mt-1 text-sm text-amber-800">Try removing filters or using one of the suggestions.</p>
                  {suggestResult.data && suggestResult.data.length > 0 ? (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {suggestResult.data.slice(0, 5).map((item) => (
                        <button
                          key={`empty-${item.text}`}
                          type="button"
                          onClick={() => setQuery(item.text)}
                          className="rounded-full border border-amber-300 bg-white px-3 py-1 text-xs text-amber-900"
                        >
                          {item.text}
                        </button>
                      ))}
                    </div>
                  ) : null}
                </div>
              ) : (
                <ul className="space-y-3">
                  {searchData.results.map((item, index) => (
                    <li key={item.id} className="rounded-lg border border-slate-200 p-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <button
                            type="button"
                            className="text-left text-base font-semibold text-slate-900 hover:underline"
                            onClick={() =>
                              void emitSearchAnalyticsEvent(
                                "search_result_click",
                                debouncedQuery,
                                item.id,
                                index + 1,
                                searchData.page,
                              )
                            }
                          >
                            {item.title}
                          </button>
                          <p className="mt-1 text-sm text-slate-600">
                            {item.brand} • {item.category}
                          </p>
                          {item.description ? (
                            <p className="mt-2 line-clamp-2 text-sm text-slate-600">{item.description}</p>
                          ) : null}
                        </div>

                        <div className="text-right">
                          <p className="text-lg font-semibold text-slate-900">
                            {item.currency} {item.price.toFixed(2)}
                          </p>
                          <p className="text-xs text-slate-500">
                            Rating {item.rating.toFixed(1)} ({item.ratingCount})
                          </p>
                          <p className="text-xs text-slate-500">Stock: {item.stockStatus}</p>
                          <button
                            type="button"
                            className="mt-2 rounded-md border border-slate-300 bg-slate-50 px-3 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100"
                            onClick={() =>
                              void emitSearchAnalyticsEvent(
                                "search_add_to_cart",
                                debouncedQuery,
                                item.id,
                                index + 1,
                                searchData.page,
                              )
                            }
                          >
                            Add to cart
                          </button>
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              {searchData.totalPages > 1 ? (
                <div className="mt-4 flex items-center justify-end gap-2">
                  <button
                    type="button"
                    disabled={searchData.page <= 1}
                    onClick={() => setPage((current) => Math.max(1, current - 1))}
                    className="rounded-md border border-slate-300 px-3 py-1 text-sm text-slate-700 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Previous
                  </button>
                  <button
                    type="button"
                    disabled={searchData.page >= searchData.totalPages}
                    onClick={() => setPage((current) => Math.min(searchData.totalPages, current + 1))}
                    className="rounded-md border border-slate-300 px-3 py-1 text-sm text-slate-700 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Next
                  </button>
                </div>
              ) : null}
            </>
          ) : null}
        </div>
      </div>
    </section>
  );
}
