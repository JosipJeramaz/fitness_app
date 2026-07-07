import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RegisterUserRequest, User } from '../models/models';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  /** Cached user list that drives the selectors across the app. */
  readonly users = signal<User[]>([]);

  loadUsers(): Observable<User[]> {
    return this.http.get<User[]>(this.baseUrl).pipe(tap((users) => this.users.set(users)));
  }

  register(request: RegisterUserRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, request);
  }
}
