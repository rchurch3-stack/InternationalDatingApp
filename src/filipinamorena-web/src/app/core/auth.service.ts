import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';

export interface AuthResponse {
  accessToken: string;
  expiresUtc: string;
  user: { id: string; email: string; displayName: string; isMfaEnabled: boolean };
}

export interface LoginRequest {
  email: string;
  password: string;
  totpCode?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/auth';

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request).pipe(
      tap(response => this.store(response))
    );
  }

  externalLogin(provider: 'google' | 'facebook'): void {
    const returnUrl = `${window.location.origin}/auth/callback`;
    window.location.assign(`${this.apiUrl}/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  completeExternalLogin(): boolean {
    const params = new URLSearchParams(window.location.hash.slice(1));
    const token = params.get('access_token');
    if (!token) return false;
    sessionStorage.setItem('access_token', token);
    history.replaceState(null, '', window.location.pathname);
    return true;
  }

  token(): string | null {
    return sessionStorage.getItem('access_token');
  }

  private store(response: AuthResponse): void {
    sessionStorage.setItem('access_token', response.accessToken);
    sessionStorage.setItem('current_user', JSON.stringify(response.user));
  }
}

export function authErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    return error.error?.message ?? 'We could not sign you in. Please try again.';
  }
  return 'We could not sign you in. Please try again.';
}
