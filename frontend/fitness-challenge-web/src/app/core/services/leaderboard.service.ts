import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LeaderboardEntry, Paged, RaceChartData } from '../models/models';

export interface LeaderboardSort {
  sortBy: 'rank' | 'trend';
  sortDir: 'asc' | 'desc';
}

@Injectable({ providedIn: 'root' })
export class LeaderboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/leaderboard`;

  /**
   * Paged ranking — same `{items, totalCount}` envelope as the activities list. Sortable by `rank`
   * (default) or `trend` (ascending hierarchy: up, same, down, new); the server owns the ordering.
   */
  getLeaderboard(
    windowDays = 7,
    limit?: number,
    offset?: number,
    sort?: LeaderboardSort,
  ): Observable<Paged<LeaderboardEntry>> {
    let params = new HttpParams().set('windowDays', windowDays);
    if (limit != null) params = params.set('limit', limit);
    if (offset != null) params = params.set('offset', offset);
    if (sort != null) params = params.set('sortBy', sort.sortBy).set('sortDir', sort.sortDir);
    return this.http.get<Paged<LeaderboardEntry>>(this.baseUrl, { params });
  }

  /** Omit `days` for the "since first activity" mode (the server anchors at the earliest charted activity). */
  getRace(days?: number, top = 5): Observable<RaceChartData> {
    let params = new HttpParams().set('top', top);
    if (days != null) {
      params = params.set('days', days);
    }
    return this.http.get<RaceChartData>(`${this.baseUrl}/race`, { params });
  }
}
