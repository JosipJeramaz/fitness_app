import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Activity, ActivityIngestResponse, IngestActivityRequest, Paged } from '../models/models';

/** Server-side sort options for a user's activity list (mirrors the API allowlist). */
export interface ActivitySort {
  sortBy: 'datetime' | 'points';
  sortDir: 'asc' | 'desc';
}

/** Server-side page window. */
export interface ActivityPageRequest {
  limit: number;
  offset: number;
}

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /**
   * Bumped after every successful ingest so open views (e.g. a dashboard behind the
   * log-activity dialog) know to refresh themselves.
   */
  readonly activityLogged = signal(0);

  ingest(request: IngestActivityRequest): Observable<ActivityIngestResponse> {
    return this.http
      .post<ActivityIngestResponse>(`${this.baseUrl}/activities`, request)
      .pipe(tap(() => this.activityLogged.update((n) => n + 1)));
  }

  delete(activityId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/activities/${activityId}`);
  }

  getForUser(userId: string, sort?: ActivitySort, page?: ActivityPageRequest): Observable<Paged<Activity>> {
    let params = new HttpParams();
    if (sort) {
      params = params.set('sortBy', sort.sortBy).set('sortDir', sort.sortDir);
    }
    if (page) {
      params = params.set('limit', page.limit).set('offset', page.offset);
    }
    return this.http.get<Paged<Activity>>(`${this.baseUrl}/users/${userId}/activities`, { params });
  }
}
