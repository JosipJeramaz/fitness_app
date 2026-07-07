import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChartConfiguration, ChartData } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ACTIVITY_LABELS, Activity, DashboardData } from '../../core/models/models';
import { ActivityService, ActivitySort } from '../../core/services/activity.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { CalendarHeatmap } from '../../shared/calendar-heatmap/calendar-heatmap';
import { CHART_COLORS } from '../../shared/chart-palette';
import { shortDate } from '../../shared/dates';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog/confirm-dialog';
import { ScoringInfoDialog } from '../../shared/scoring-info-dialog/scoring-info-dialog';

const BASE_COLUMNS = ['datetime', 'type', 'metric', 'points'];

@Component({
  selector: 'app-dashboard',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatCardModule,
    MatDialogModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    BaseChartDirective,
    CalendarHeatmap,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly dashboardService = inject(DashboardService);
  private readonly activityService = inject(ActivityService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** Bound from the `:userId` route parameter via component input binding. */
  readonly userId = input.required<string>();

  readonly data = signal<DashboardData | null>(null);
  readonly activities = signal<Activity[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly sort = signal<ActivitySort>({ sortBy: 'datetime', sortDir: 'desc' });

  // Server-side paging state for the history table.
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly pageSizeOptions = [5, 10, 25];

  /** Deleting is self-only, so the actions column exists only on one's own dashboard. */
  readonly isOwnDashboard = computed(() => this.userId() === this.authService.userId());
  readonly activityColumns = computed(() =>
    this.isOwnDashboard() ? [...BASE_COLUMNS, 'actions'] : BASE_COLUMNS,
  );
  readonly labels = ACTIVITY_LABELS;

  readonly lineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      // Thin out the date labels (like the race chart) so a long history isn't crowded.
      x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
      y: { beginAtZero: true, ticks: { precision: 0 } },
    },
  };

  readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' } },
  };

  readonly pointsChart = computed<ChartData<'line'>>(() => {
    const points = this.data()?.pointsOverTime ?? [];
    return {
      labels: points.map((p) => shortDate(p.date)),
      datasets: [
        {
          data: points.map((p) => p.points),
          label: 'Points',
          fill: true,
          tension: 0.35,
          borderColor: CHART_COLORS[0],
          backgroundColor: 'rgba(63, 111, 216, 0.15)',
          pointBackgroundColor: CHART_COLORS[0],
        },
      ],
    };
  });

  readonly sportChart = computed<ChartData<'doughnut'>>(() => {
    const breakdown = this.data()?.sportBreakdown ?? [];
    return {
      labels: breakdown.map((b) => this.labels[b.activityType]),
      datasets: [{ data: breakdown.map((b) => b.points), backgroundColor: CHART_COLORS }],
    };
  });

  constructor() {
    effect(() => {
      const userId = this.userId();
      // Also re-run after the log-activity dialog ingests a new entry, so an open dashboard shows it.
      this.activityService.activityLogged();
      if (userId) {
        // untracked: load() reads paging/sort signals internally; without this the effect would adopt
        // them as dependencies, re-run on every page/sort change, and reset the paginator to page 0.
        untracked(() => this.load(userId));
      }
    });
  }

  labelFor(activityType: string): string {
    return ACTIVITY_LABELS[activityType as keyof typeof ACTIVITY_LABELS] ?? activityType;
  }

  /** Drives the always-visible ▲▼ buttons: is the given column currently sorted in the given direction? */
  isSorted(column: 'datetime' | 'points', direction: 'asc' | 'desc'): boolean {
    const current = this.sort();
    return current.sortBy === column && current.sortDir === direction;
  }

  /** Screen-reader state for the column header. */
  ariaSort(column: 'datetime' | 'points'): string {
    const current = this.sort();
    if (current.sortBy !== column) return 'none';
    return current.sortDir === 'asc' ? 'ascending' : 'descending';
  }

  /** Arrow button click → explicit column + direction, re-fetched from the server which owns ordering. */
  setSort(sortBy: 'datetime' | 'points', sortDir: 'asc' | 'desc'): void {
    const current = this.sort();
    if (current.sortBy === sortBy && current.sortDir === sortDir) return; // already sorted this way

    this.sort.set({ sortBy, sortDir });
    // A new ordering redefines what "page 3" means — always jump back to the first page with it.
    this.pageIndex.set(0);
    this.loadActivities(this.userId());
  }

  onPageChange(event: PageEvent): void {
    // Changing the page size also re-frames the pages, so restart from the first one.
    if (event.pageSize !== this.pageSize()) {
      this.pageSize.set(event.pageSize);
      this.pageIndex.set(0);
    } else {
      this.pageIndex.set(event.pageIndex);
    }
    this.loadActivities(this.userId());
  }

  openScoring(): void {
    this.dialog.open(ScoringInfoDialog, { width: '380px' });
  }

  confirmDelete(activity: Activity): void {
    const data: ConfirmDialogData = {
      title: 'Delete activity?',
      message: `This permanently removes this ${this.labelFor(activity.activityType)} entry (${activity.points} points).`,
      confirmLabel: 'Delete',
    };
    this.dialog
      .open(ConfirmDialog, { width: '420px', data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.deleteActivity(activity);
      });
  }

  deleteActivity(activity: Activity): void {
    this.activityService.delete(activity.id).subscribe({
      next: () => {
        this.snackBar.open('Activity deleted.', 'Close', { duration: 3000 });
        this.refreshAfterDelete();
      },
      error: (err: HttpErrorResponse) => {
        this.snackBar.open(
          err.status === 404 ? 'That activity was already deleted.' : 'Could not delete the activity.',
          'Close',
          { duration: 4000 },
        );
        // A 404 means the row is gone anyway (e.g. deleted in another tab) — resync the view.
        if (err.status === 404) this.refreshAfterDelete();
      },
    });
  }

  private refreshAfterDelete(): void {
    // Deleting the only row of a later page: step back so we don't fetch an empty page.
    if (this.activities().length === 1 && this.pageIndex() > 0) {
      this.pageIndex.set(this.pageIndex() - 1);
    }
    this.loadActivities(this.userId());
    this.refreshSummary(this.userId());
  }

  /** Re-fetches totals/charts without resetting paging or flashing the full-page spinner (unlike load()). */
  private refreshSummary(userId: string): void {
    this.dashboardService.getDashboard(userId).subscribe({
      next: (data) => this.data.set(data),
    });
  }

  metricLabel(activity: Activity): string {
    if (activity.distanceKm != null) return `${activity.distanceKm} km`;
    if (activity.durationSeconds != null) {
      const minutes = Math.floor(activity.durationSeconds / 60);
      const seconds = activity.durationSeconds % 60;
      return `${minutes}:${seconds.toString().padStart(2, '0')} min`;
    }
    if (activity.steps != null) return `${activity.steps} steps`;
    return '—';
  }

  private load(userId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.pageIndex.set(0); // a different user means a different data set — restart paging

    this.dashboardService.getDashboard(userId).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(err.status === 404 ? 'That user could not be found.' : 'Could not load the dashboard.');
        this.loading.set(false);
      },
    });

    this.loadActivities(userId);
  }

  private loadActivities(userId: string): void {
    const page = { limit: this.pageSize(), offset: this.pageIndex() * this.pageSize() };
    this.activityService.getForUser(userId, this.sort(), page).subscribe({
      next: (result) => {
        this.activities.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => {
        this.activities.set([]);
        this.totalCount.set(0);
      },
    });
  }
}
