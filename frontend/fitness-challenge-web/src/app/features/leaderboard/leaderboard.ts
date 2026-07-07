import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ChartConfiguration, ChartData } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { LeaderboardEntry, RaceChartData } from '../../core/models/models';
import { ActivityService } from '../../core/services/activity.service';
import { LeaderboardService, LeaderboardSort } from '../../core/services/leaderboard.service';
import { CHART_COLORS } from '../../shared/chart-palette';
import { longDate, shortDate } from '../../shared/dates';
import { TrendIndicator } from '../../shared/trend-indicator/trend-indicator';

const RACE_TOP = 5;

/** 'all' = since the charted users' first activity (server-anchored); numbers = fixed day windows. */
export type RaceRange = 'all' | 7 | 30;

@Component({
  selector: 'app-leaderboard',
  imports: [
    DecimalPipe,
    MatCardModule,
    MatTableModule,
    MatButtonToggleModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    BaseChartDirective,
    TrendIndicator,
  ],
  templateUrl: './leaderboard.html',
  styleUrl: './leaderboard.scss',
})
export class Leaderboard {
  private readonly leaderboardService = inject(LeaderboardService);
  private readonly activityService = inject(ActivityService);
  private readonly router = inject(Router);

  readonly entries = signal<LeaderboardEntry[]>([]);
  /** True only until the first table response — later loads swap data in place, without a spinner. */
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly windowDays = signal(7);
  readonly race = signal<RaceChartData | null>(null);
  readonly raceRange = signal<RaceRange>('all');

  // Server-side paging + sorting state — same contract as the dashboard's activity history.
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly pageSizeOptions = [5, 10, 25];
  readonly sort = signal<LeaderboardSort>({ sortBy: 'rank', sortDir: 'asc' });

  readonly displayedColumns = ['rank', 'name', 'points', 'trend'] as const;
  readonly raceTop = RACE_TOP;

  /** Year (or year span) of the visible race window — keeps the x-axis labels year-free. */
  readonly raceYearLabel = computed(() => {
    const dates = this.race()?.dates ?? [];
    if (!dates.length) return '';
    const first = dates[0].slice(0, 4);
    const last = dates[dates.length - 1].slice(0, 4);
    return first === last ? first : `${first}–${last}`;
  });

  readonly raceChartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom', labels: { usePointStyle: true, boxHeight: 8 } },
      tooltip: {
        // Rank the tooltip rows like the race standings at that moment.
        itemSort: (a, b) => (b.parsed.y ?? 0) - (a.parsed.y ?? 0),
        callbacks: {
          // The full date (with year) lives here, so the axis can stay compact.
          title: (items) => (items[0] ? longDate(items[0].label) : ''),
        },
      },
    },
    interaction: { mode: 'index', intersect: false },
    scales: {
      x: {
        grid: { display: false },
        ticks: {
          maxRotation: 0, // horizontal labels
          autoSkip: true,
          maxTicksLimit: 8,
          callback(value) {
            return shortDate(this.getLabelForValue(value as number));
          },
        },
      },
      y: { beginAtZero: true, ticks: { precision: 0 } },
    },
  };

  readonly raceChart = computed<ChartData<'line'>>(() => {
    const race = this.race();
    const lastIndex = (race?.dates.length ?? 0) - 1;
    return {
      labels: race?.dates ?? [],
      datasets: (race?.series ?? []).map((series, i) => {
        // The line exists only from the athlete's first activity (leading buckets are null).
        const entryIndex = series.cumulativePoints.findIndex((value) => value != null);
        return {
          data: series.cumulativePoints,
          label: `${series.firstName} ${series.lastName}`,
          borderColor: CHART_COLORS[i % CHART_COLORS.length],
          backgroundColor: CHART_COLORS[i % CHART_COLORS.length],
          borderWidth: i === 0 ? 3 : 2, // the current leader's line is slightly bolder
          // Dots mark where the athlete entered the race and where they stand now.
          pointRadius: series.cumulativePoints.map((_, idx) => (idx === entryIndex || idx === lastIndex ? 4 : 0)),
          pointHoverRadius: 5,
          tension: 0.3,
          spanGaps: false, // null = no line yet; never bridge across the gap
          fill: false,
        };
      }),
    };
  });

  constructor() {
    effect(() => {
      // Initial load, and a re-fetch after any activity is logged (e.g. from the toolbar dialog while
      // this page is open) so new points show up immediately. untracked: the loads read paging/sort/range
      // signals internally — without it the effect would adopt them and re-run on every page or sort change.
      this.activityService.activityLogged();
      untracked(() => {
        this.load();
        this.loadRace();
      });
    });
  }

  onWindowChange(days: number): void {
    // The window only redefines the trend column, not the ranking, so the current page stays valid.
    // The race chart has its own range control and is untouched.
    this.windowDays.set(days);
    this.load();
  }

  onPageChange(event: PageEvent): void {
    // Changing the page size also re-frames the pages, so restart from the first one.
    if (event.pageSize !== this.pageSize()) {
      this.pageSize.set(event.pageSize);
      this.pageIndex.set(0);
    } else {
      this.pageIndex.set(event.pageIndex);
    }
    this.load();
  }

  /** Arrow button click → explicit column + direction, re-fetched from the server which owns ordering. */
  setSort(sortBy: 'rank' | 'trend', sortDir: 'asc' | 'desc'): void {
    const current = this.sort();
    if (current.sortBy === sortBy && current.sortDir === sortDir) return; // already sorted this way

    this.sort.set({ sortBy, sortDir });
    // A new ordering redefines what "page 3" means — always jump back to the first page with it.
    this.pageIndex.set(0);
    this.load();
  }

  /** Drives the always-visible ▲▼ buttons: is the given column currently sorted in the given direction? */
  isSorted(column: 'rank' | 'trend', direction: 'asc' | 'desc'): boolean {
    const current = this.sort();
    return current.sortBy === column && current.sortDir === direction;
  }

  /** Screen-reader state for the column header. */
  ariaSort(column: 'rank' | 'trend'): string {
    const current = this.sort();
    if (current.sortBy !== column) return 'none';
    return current.sortDir === 'asc' ? 'ascending' : 'descending';
  }

  onRaceRangeChange(range: RaceRange): void {
    this.raceRange.set(range);
    this.loadRace();
  }

  openDashboard(userId: string): void {
    this.router.navigate(['/dashboard', userId]);
  }

  /** Loads the table only — the race chart lives its own life so paging never re-renders it. */
  private load(): void {
    this.error.set(null);

    const offset = this.pageIndex() * this.pageSize();
    this.leaderboardService.getLeaderboard(this.windowDays(), this.pageSize(), offset, this.sort()).subscribe({
      next: (page) => {
        this.entries.set(page.items);
        this.totalCount.set(page.totalCount);
        this.loading.set(false);
      },
      error: (_err: HttpErrorResponse) => {
        this.error.set('Could not load the leaderboard. Is the API running?');
        this.loading.set(false);
      },
    });
  }

  private loadRace(): void {
    const range = this.raceRange();
    this.leaderboardService.getRace(range === 'all' ? undefined : range, RACE_TOP).subscribe({
      next: (race) => this.race.set(race),
      error: () => this.race.set(null), // the race chart is a bonus — never block the table on it
    });
  }
}
