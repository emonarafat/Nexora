import type { SuggestionItem } from "../../types/search";
import type { PriceRange } from "../../hooks/useSearch";

interface SearchInputProps {
  query: string;
  onQueryChange: (q: string) => void;
  priceRange: PriceRange;
  onPriceRangeChange: (range: PriceRange) => void;
  hasSearchInput: boolean;
  suggestions: SuggestionItem[] | undefined;
  onSuggestionSelect: (text: string) => void;
}

export function SearchInput({
  query,
  onQueryChange,
  priceRange,
  onPriceRangeChange,
  hasSearchInput,
  suggestions,
  onSuggestionSelect,
}: SearchInputProps) {
  return (
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
            onChange={(event) => onQueryChange(event.target.value)}
            placeholder="Try: running shoes"
          />
          {!hasSearchInput ? (
            <p className="mt-2 text-xs text-slate-500">Type at least 2 characters to search.</p>
          ) : null}

          {hasSearchInput && suggestions && suggestions.length > 0 ? (
            <ul
              role="listbox"
              aria-label="Search suggestions"
              className="mt-2 rounded-md border border-slate-200 bg-slate-50 p-2 text-sm"
            >
              {suggestions.map((item) => (
                <li key={`${item.text}-${item.category ?? "uncategorized"}`} role="option" aria-selected={false}>
                  <button
                    type="button"
                    onClick={() => onSuggestionSelect(item.text)}
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
            onChange={(event) => onPriceRangeChange(event.target.value as PriceRange)}
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
  );
}
