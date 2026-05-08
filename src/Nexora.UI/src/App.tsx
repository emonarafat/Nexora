import { Link, Route, Routes } from "react-router-dom";

function SearchPage() {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-xl font-semibold text-slate-900">Search UI</h2>
      <p className="mt-2 text-sm text-slate-600">
        Baseline shell for suggest, facets, and result rendering.
      </p>
    </section>
  );
}

function AdminPage() {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-xl font-semibold text-slate-900">Admin Dashboard</h2>
      <p className="mt-2 text-sm text-slate-600">
        Baseline shell for synonyms, reindex, and ranking configuration.
      </p>
    </section>
  );
}

export default function App() {
  return (
    <main className="min-h-screen bg-slate-50 p-6 md:p-10">
      <div className="mx-auto max-w-5xl space-y-6">
        <header className="space-y-2">
          <h1 className="text-3xl font-bold text-slate-900">Nexora UI</h1>
          <p className="text-slate-600">
            React + TypeScript baseline scaffold for Search and Admin experiences.
          </p>
        </header>

        <nav aria-label="Primary" className="flex gap-3">
          <Link
            to="/"
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
          >
            Search
          </Link>
          <Link
            to="/admin"
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
          >
            Admin
          </Link>
        </nav>

        <Routes>
          <Route path="/" element={<SearchPage />} />
          <Route path="/admin" element={<AdminPage />} />
        </Routes>
      </div>
    </main>
  );
}
