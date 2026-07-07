import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardData } from '../models/models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getDashboard(userId: string, groupBy: 'day' | 'week' = 'day'): Observable<DashboardData> {
    const params = new HttpParams().set('groupBy', groupBy);
    return this.http.get<DashboardData>(`${this.baseUrl}/users/${userId}/dashboard`, { params });
  }
}
