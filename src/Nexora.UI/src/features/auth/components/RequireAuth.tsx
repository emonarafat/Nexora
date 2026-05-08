import { type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthContext } from '../../../contexts/AuthContext';
import type { AuthUser } from '../types';

interface RequireAuthProps {
  children: ReactNode;
  /** Optional role gate. Redirects to /unauthorized when role doesn't match. */
  requiredRole?: AuthUser['role'];
}

export function RequireAuth({ children, requiredRole }: RequireAuthProps) {
  const { isAuthenticated, user } = useAuthContext();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (requiredRole && user?.role !== requiredRole) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <>{children}</>;
}
