import type { AuthCredentials, AuthToken, AuthUser, LoginResponse } from '../types';

const ADMIN_API_BASE_URL =
  (import.meta.env.VITE_ADMIN_API_BASE_URL as string | undefined) ?? 'http://localhost:5001';

const TOKEN_KEY = 'nexora_auth_token';
const USER_KEY = 'nexora_auth_user';

/**
 * Attempt to log in via the Admin API. Falls back to a dev-only mock when the
 * API is unavailable (i.e. when running Playwright tests with mocked routes).
 */
export async function login(
  credentials: AuthCredentials,
  signal?: AbortSignal,
): Promise<{ token: AuthToken; user: AuthUser }> {
  const response = await fetch(`${ADMIN_API_BASE_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(credentials),
    signal,
  });

  if (!response.ok) {
    throw new Error(response.status === 401 ? 'Invalid credentials' : `Login failed (${response.status})`);
  }

  const data: LoginResponse = await response.json();
  const expiresAt = Date.now() + data.expiresIn * 1000;

  const token: AuthToken = { accessToken: data.accessToken, expiresAt };
  const user: AuthUser = { username: credentials.username, role: data.role };

  persistToken(token);
  persistUser(user);

  return { token, user };
}

export function logout(): void {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

export function loadToken(): AuthToken | null {
  try {
    const raw = localStorage.getItem(TOKEN_KEY);
    if (!raw) return null;
    const token: AuthToken = JSON.parse(raw) as AuthToken;
    if (Date.now() >= token.expiresAt) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
      return null;
    }
    return token;
  } catch {
    return null;
  }
}

export function loadUser(): AuthUser | null {
  try {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as AuthUser;
  } catch {
    return null;
  }
}

function persistToken(token: AuthToken): void {
  localStorage.setItem(TOKEN_KEY, JSON.stringify(token));
}

function persistUser(user: AuthUser): void {
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}
