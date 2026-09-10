import { apiRequest } from './httpClient';
import type { AuthResponse, LoginRequest, RegisterRequest } from '../types/auth';

export function login(request: LoginRequest) {
  return apiRequest<AuthResponse>('api/auth/login', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export function register(request: RegisterRequest) {
  return apiRequest<{ email: string; requiresEmailVerification: boolean }>('api/auth/register', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export const verifyEmail = (email: string, code: string) => apiRequest<AuthResponse>('api/auth/verify-email', {
  method: 'POST', body: JSON.stringify({ email, code }),
});
export const sendCode = (email: string, reset = false) => apiRequest<{ message: string }>(`api/auth/${reset ? 'forgot-password' : 'resend-verification'}`, {
  method: 'POST', body: JSON.stringify({ email }),
});
export const resetPassword = (email: string, code: string, newPassword: string) => apiRequest<{ message: string }>('api/auth/reset-password', {
  method: 'POST', body: JSON.stringify({ email, code, newPassword }),
});
export const refreshSession = (refreshToken: string) => apiRequest<AuthResponse>('api/auth/refresh', {
  method: 'POST', body: JSON.stringify({ refreshToken }),
});
export const logout = (accessToken: string) => apiRequest<void>('api/auth/logout', { method: 'POST', accessToken });
