import { Link } from 'react-router-dom';

export function UnauthorizedPage() {
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-bold text-slate-900">Access Denied</h1>
      <p className="text-sm text-slate-600">You do not have permission to view this page.</p>
      <Link
        to="/"
        className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
      >
        Back to Search
      </Link>
    </div>
  );
}
