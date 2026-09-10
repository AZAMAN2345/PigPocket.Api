export type AuthMode = 'login' | 'signup';

export type AuthResponse = {
  token: string;
  refreshToken: string;
  expiresIn: number;
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
};

export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = LoginRequest & {
  firstName: string;
  lastName: string;
};
