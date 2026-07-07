import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

interface LoginResponse {
  token: string;
  userId: string;
  expiresAt: string;
}

/**
 * Minimal client-side auth. Login is username + password (set at registration); the token and userId are
 * persisted so the interceptor can attach the token and the toolbar can reflect the signed-in user.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  readonly token = signal<string | null>(this.read('token'));
  readonly userId = signal<string | null>(this.read('userId'));

  login(userName: string, password: string): Observable<LoginResponse> {
    return this.post({ userName, password });
  }

  /** Passwordless fallback — the API only allows it for users who never set credentials. */
  loginByName(firstName: string, lastName: string): Observable<LoginResponse> {
    return this.post({ firstName, lastName });
  }

  private post(body: object): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, body).pipe(
      tap((response) => {
        this.token.set(response.token);
        this.userId.set(response.userId);
        this.write('token', response.token);
        this.write('userId', response.userId);
      }),
    );
  }

  logout(): void {
    this.token.set(null);
    this.userId.set(null);
    this.write('token', null);
    this.write('userId', null);
  }

  private read(key: string): string | null {
    return typeof localStorage !== 'undefined' ? localStorage.getItem(key) : null;
  }

  private write(key: string, value: string | null): void {
    if (typeof localStorage === 'undefined') return;
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  }
}
