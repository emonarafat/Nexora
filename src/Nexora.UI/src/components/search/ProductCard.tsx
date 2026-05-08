import type { ProductResult } from "../../types/search";

interface ProductCardProps {
  item: ProductResult;
  position: number;
  onResultClick: (item: ProductResult, position: number) => void;
  onAddToCart: (item: ProductResult, position: number) => void;
}

export function ProductCard({ item, position, onResultClick, onAddToCart }: ProductCardProps) {
  return (
    <li className="rounded-lg border border-slate-200 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <button
            type="button"
            className="text-left text-base font-semibold text-slate-900 hover:underline"
            onClick={() => onResultClick(item, position)}
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
            aria-label={`Add ${item.title} to cart`}
            className="mt-2 rounded-md border border-slate-300 bg-slate-50 px-3 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100"
            onClick={() => onAddToCart(item, position)}
          >
            Add to cart
          </button>
        </div>
      </div>
    </li>
  );
}
