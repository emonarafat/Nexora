export interface AuthCredentials {
  username: string;
  password: string;
}

export interface AuthToken {
  accessToken: string;
  expiresAt: number; // Unix timestamp in ms
}

export interface AuthUser {
  username: string;
  role: 'admin' | 'viewer';
}

export interface LoginResponse {
  accessToken: string;
  expiresIn: number; // seconds
  role: 'admin' | 'viewer';
}
