import { emitSearchAnalyticsEvent } from "../services/analyticsService";
import { useSearch } from "../hooks/useSearch";
import { SearchInput } from "../components/search/SearchInput";
import { FacetsPanel } from "../components/search/FacetsPanel";
import { ProductCard } from "../components/search/ProductCard";
import { Pagination } from "../components/search/Pagination";
import type { ProductResult, FacetValue } from "../types/search";

function getFacetValues(facets: Record<string, FacetValue[]> | undefined, key: string): FacetValue[] {
  return (facets?.[key] ?? []).filter((item) => item.count > 0);
}

export function SearchPage() {
  const {
    query,
    setQuery,
    debouncedQuery,
    hasSearchInput,
    page,
    setPage,
    selectedBrands,
    selectedCategories,
    priceRange,
    setPriceRange,
    toggleBrand,
    toggleCategory,
    searchResult,
    suggestResult,
  } = useSearch();

  const searchData = searchResult.data;
  const brandFacets = getFacetValues(searchData?.facets, "brand");
  const categoryFacets = getFacetValues(searchData?.facets, "category");

  const handleResultClick = (item: ProductResult, position: number) => {
    void emitSearchAnalyticsEvent("search_result_click", debouncedQuery, item.id, position, searchData?.page ?? 1);
  };

  const handleAddToCart = (item: ProductResult, position: number) => {
    void emitSearchAnalyticsEvent("search_add_to_cart", debouncedQuery, item.id, position, searchData?.page ?? 1);
  };

  return (
    <section className="space-y-4">
      <SearchInput
        query={query}
        onQueryChange={setQuery}
        priceRange={priceRange}
        onPriceRangeChange={setPriceRange}
        hasSearchInput={hasSearchInput}
        suggestions={suggestResult.data}
        onSuggestionSelect={setQuery}
      />

      <div className="grid gap-4 md:grid-cols-[260px_1fr]">
        <FacetsPanel
          brandFacets={brandFacets}
          categoryFacets={categoryFacets}
          selectedBrands={selectedBrands}
          selectedCategories={selectedCategories}
          onToggleBrand={toggleBrand}
          onToggleCategory={toggleCategory}
        />

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
          {searchResult.isPending ? (
            <p className="text-sm text-slate-600">Loading search results...</p>
          ) : null}

          {searchResult.isError ? (
            <p role="alert" className="text-sm text-red-700">Search API unavailable. Please try again.</p>
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
                  <p className="mt-1 text-sm text-amber-800">
                    Try removing filters or using one of the suggestions.
                  </p>
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
                    <ProductCard
                      key={item.id}
                      item={item}
                      position={index + 1}
                      onResultClick={handleResultClick}
                      onAddToCart={handleAddToCart}
                    />
                  ))}
                </ul>
              )}

              <Pagination
                page={searchData.page}
                totalPages={searchData.totalPages}
                onPrev={() => setPage(Math.max(1, page - 1))}
                onNext={() => setPage(Math.min(searchData.totalPages, page + 1))}
              />
            </>
          ) : null}
        </div>
      </div>
    </section>
  );
}
