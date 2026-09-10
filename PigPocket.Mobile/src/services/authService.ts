import { apiRequest } from './httpClient';
import type { AuthResponse, LoginRequest, RegisterRequest } from '../types/auth';

export function login(request: LoginRequest) {
  return apiRequest<AuthResponse>('api/auth/login', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export function register(request: RegisterRequest) {
  return apiRequest<AuthResponse>('api/auth/register', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}
