import type { FacetValue } from "../../types/search";

interface FacetsPanelProps {
  brandFacets: FacetValue[];
  categoryFacets: FacetValue[];
  selectedBrands: string[];
  selectedCategories: string[];
  onToggleBrand: (brand: string) => void;
  onToggleCategory: (category: string) => void;
}

export function FacetsPanel({
  brandFacets,
  categoryFacets,
  selectedBrands,
  selectedCategories,
  onToggleBrand,
  onToggleCategory,
}: FacetsPanelProps) {
  return (
    <aside className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
      <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-700">Facets</h3>

      <div className="mt-4 space-y-4">
        <div>
          <p className="mb-2 text-sm font-medium text-slate-800">Brand</p>
          <div className="space-y-1">
            {brandFacets.length === 0 ? (
              <p className="text-xs text-slate-500">No brand facets yet.</p>
            ) : (
              brandFacets.map((item) => (
                <label key={item.value} className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={selectedBrands.includes(item.value)}
                    onChange={() => onToggleBrand(item.value)}
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
            {categoryFacets.length === 0 ? (
              <p className="text-xs text-slate-500">No category facets yet.</p>
            ) : (
              categoryFacets.map((item) => (
                <label key={item.value} className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={selectedCategories.includes(item.value)}
                    onChange={() => onToggleCategory(item.value)}
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
  );
}
