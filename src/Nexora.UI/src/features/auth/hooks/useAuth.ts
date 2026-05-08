import { useState, useCallback } from 'react';
import { login as apiLogin, logout as apiLogout, loadToken, loadUser } from '../services/authService';
import type { AuthCredentials, AuthUser } from '../types';

export interface UseAuthReturn {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  login: (credentials: AuthCredentials) => Promise<void>;
  logout: () => void;
}

export function useAuth(): UseAuthReturn {
  const [user, setUser] = useState<AuthUser | null>(() => {
    // Restore persisted session on mount
    const token = loadToken();
    if (!token) return null;
    return loadUser();
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const login = useCallback(async (credentials: AuthCredentials) => {
    setIsLoading(true);
    setError(null);
    try {
      const { user: loggedInUser } = await apiLogin(credentials);
      setUser(loggedInUser);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    apiLogout();
    setUser(null);
    setError(null);
  }, []);

  return {
    user,
    isAuthenticated: user !== null,
    isLoading,
    error,
    login,
    logout,
  };
}
