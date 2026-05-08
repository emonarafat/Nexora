import { Link, Route, Routes } from "react-router-dom";
import { AdminPage } from "./pages/AdminPage";
import { SearchPage } from "./pages/SearchPage";
import { LoginPage } from "./pages/LoginPage";
import { UnauthorizedPage } from "./pages/UnauthorizedPage";
import { RequireAuth } from "./features/auth/components/RequireAuth";
import { useAuthContext } from "./contexts/AuthContext";

export default function App() {
  const { isAuthenticated, user, logout } = useAuthContext();

  return (
    <main className="min-h-screen bg-slate-50 p-6 md:p-10">
      <div className="mx-auto max-w-5xl space-y-6">
        <header className="space-y-2">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-slate-900">Nexora UI</h1>
              <p className="text-slate-600">
                React + TypeScript baseline scaffold for Search and Admin experiences.
              </p>
            </div>
            {isAuthenticated ? (
              <div className="flex items-center gap-3">
                <span className="text-sm text-slate-600">
                  {user?.username}{" "}
                  <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800">
                    {user?.role}
                  </span>
                </span>
                <button
                  type="button"
                  onClick={logout}
                  className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-100"
                >
                  Sign out
                </button>
              </div>
            ) : null}
          </div>
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
          <Route path="/login" element={<LoginPage />} />
          <Route path="/unauthorized" element={<UnauthorizedPage />} />
          <Route
            path="/admin"
            element={
              <RequireAuth requiredRole="admin">
                <AdminPage />
              </RequireAuth>
            }
          />
        </Routes>
      </div>
    </main>
  );
}
