import { Component, computed, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PointsOverTimePoint } from '../../core/models/models';
import { shortDate } from '../dates';

interface HeatCell {
  date: string;
  level: number; // 0 = no activity, 1–4 = points relative to the best day
  future: boolean;
  tooltip: string;
}

interface MonthLabel {
  column: number;
  label: string;
}

const DAY_MS = 86_400_000;
const WEEKS_BACK_DAYS = 364; // rolling 12 months, GitHub-style

/**
 * GitHub-style calendar heatmap of the last 12 months of activity. Columns are weeks
 * (Monday-first, matching the backend's week buckets); cell intensity is the day's points
 * relative to the user's best day in the window. Pure CSS grid — no chart dependency.
 */
@Component({
  selector: 'app-calendar-heatmap',
  imports: [MatTooltipModule],
  template: `
    <div class="heatmap">
      <div class="months">
        @for (month of view().monthLabels; track month.column) {
          <span class="month" [style.left.%]="(month.column / view().weeks.length) * 100">{{ month.label }}</span>
        }
      </div>
      <div class="body">
        <div class="day-labels" aria-hidden="true">
          <span>Mon</span>
          <span>Wed</span>
          <span>Fri</span>
        </div>
        <div class="weeks">
          @for (week of view().weeks; track $index) {
            <div class="week">
              @for (cell of week; track cell.date) {
                @if (cell.future) {
                  <span class="cell cell--future"></span>
                } @else {
                  <span
                    class="cell level-{{ cell.level }}"
                    role="img"
                    [attr.aria-label]="cell.tooltip"
                    [matTooltip]="cell.tooltip"
                  ></span>
                }
              }
            </div>
          }
        </div>
      </div>
      <div class="legend" aria-hidden="true">
        <span>Less</span>
        <span class="cell level-0"></span>
        <span class="cell level-1"></span>
        <span class="cell level-2"></span>
        <span class="cell level-3"></span>
        <span class="cell level-4"></span>
        <span>More</span>
      </div>
      <div class="stats">
        <div class="stat">
          <span class="stat__value">{{ stats().currentStreak }} {{ stats().currentStreak === 1 ? 'day' : 'days' }}</span>
          <span class="stat__label">Current streak</span>
        </div>
        <div class="stat">
          <span class="stat__value">{{ stats().longestStreak }} {{ stats().longestStreak === 1 ? 'day' : 'days' }}</span>
          <span class="stat__label">Longest streak</span>
        </div>
        <div class="stat">
          <span class="stat__value">{{ stats().activeDays }}</span>
          <span class="stat__label">Active days in the last year</span>
        </div>
        <div class="stat">
          <span class="stat__value">{{ stats().bestValue }}</span>
          <span class="stat__label">{{ stats().bestLabel }}</span>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .heatmap {
        display: flex;
        flex-direction: column;
        width: 100%;
        font-size: 0.72rem;
        color: var(--mat-sys-on-surface-variant);
      }
      .months {
        position: relative;
        height: 16px;
        margin-left: 32px;
      }
      .month {
        position: absolute;
        top: 0;
        white-space: nowrap;
      }
      .body {
        display: flex;
        gap: 6px;
      }
      .day-labels {
        display: flex;
        flex-direction: column;
        justify-content: space-between;
        width: 26px;
        padding: 1px 0;
        text-align: right;
        flex-shrink: 0;
      }
      /* Cells flex to the card width so the full year always fits without scrolling. */
      .weeks {
        display: flex;
        flex: 1;
        min-width: 0;
        gap: 2px;
      }
      .week {
        flex: 1 1 0;
        min-width: 0;
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .cell {
        width: 100%;
        aspect-ratio: 1 / 1;
        border-radius: 2px;
        background: color-mix(in srgb, var(--mat-sys-outline) 16%, transparent);
      }
      .legend .cell {
        width: 11px;
      }
      .cell--future {
        background: transparent;
      }
      .level-1 {
        background: color-mix(in srgb, var(--mat-sys-primary) 28%, transparent);
      }
      .level-2 {
        background: color-mix(in srgb, var(--mat-sys-primary) 52%, transparent);
      }
      .level-3 {
        background: color-mix(in srgb, var(--mat-sys-primary) 76%, transparent);
      }
      .level-4 {
        background: var(--mat-sys-primary);
      }
      .legend {
        display: flex;
        align-items: center;
        gap: 3px;
        justify-content: flex-end;
        margin-top: 6px;
      }
      .legend span:first-child {
        margin-right: 2px;
      }
      .legend span:last-child {
        margin-left: 2px;
      }
      .stats {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 12px;
        margin-top: 20px;
        padding-top: 16px;
        border-top: 1px solid color-mix(in srgb, var(--mat-sys-outline) 25%, transparent);
      }
      .stat {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
        text-align: center;
      }
      .stat__value {
        font-size: 1.35rem;
        font-weight: 600;
        color: var(--mat-sys-on-surface);
        font-variant-numeric: tabular-nums;
      }
      .stat__label {
        font-size: 0.72rem;
      }
      @media (max-width: 640px) {
        .stats {
          grid-template-columns: repeat(2, 1fr);
        }
      }
    `,
  ],
})
export class CalendarHeatmap {
  /** Day buckets from the dashboard (`groupBy=day`); days without activity may be absent. */
  readonly points = input.required<PointsOverTimePoint[]>();
  /** Last day of the window as `yyyy-MM-dd`; defaults to today (overridable for tests). */
  readonly endDate = input<string | null>(null);

  /** The rolling 12-month window: [Monday-aligned start, end day]. */
  private readonly window = computed(() => {
    const end = this.endDate()
      ? new Date(`${this.endDate()}T00:00:00Z`)
      : new Date(`${new Date().toISOString().slice(0, 10)}T00:00:00Z`);
    const start = mondayOf(new Date(end.getTime() - WEEKS_BACK_DAYS * DAY_MS));
    return { start, end };
  });

  readonly view = computed(() => {
    const byDate = new Map(this.points().map((p) => [p.date, p]));
    const { start, end } = this.window();

    // The scale is relative to the best day inside the window only.
    let maxPoints = 0;
    for (let day = start; day <= end; day = addDays(day, 1)) {
      maxPoints = Math.max(maxPoints, byDate.get(isoDate(day))?.points ?? 0);
    }

    const weeks: HeatCell[][] = [];
    const monthLabels: MonthLabel[] = [];
    let previousMonth = -1;

    for (let monday = start; monday <= end; monday = addDays(monday, 7)) {
      if (monday.getUTCMonth() !== previousMonth) {
        previousMonth = monday.getUTCMonth();
        monthLabels.push({ column: weeks.length, label: MONTHS[previousMonth] });
      }

      const week: HeatCell[] = [];
      for (let offset = 0; offset < 7; offset++) {
        const day = addDays(monday, offset);
        const date = isoDate(day);
        if (day > end) {
          week.push({ date, level: 0, future: true, tooltip: '' });
          continue;
        }
        const bucket = byDate.get(date);
        week.push({
          date,
          level: levelFor(bucket?.points ?? 0, maxPoints),
          future: false,
          tooltip: tooltipFor(date, bucket),
        });
      }
      weeks.push(week);
    }

    // Drop a cramped first label when the next month starts within the first two columns.
    if (monthLabels.length > 1 && monthLabels[1].column <= 2) {
      monthLabels.shift();
    }

    return { weeks, monthLabels };
  });

  /**
   * Streak & summary tiles below the calendar. Streaks use the full history (an active
   * day = any logged activity); active days and best day are scoped to the visible
   * 12-month window, matching what the calendar shows.
   */
  readonly stats = computed(() => {
    const { start, end } = this.window();
    const activeDates = new Set(this.points().filter((p) => p.activityCount > 0).map((p) => p.date));

    // Current streak counts back from today; a quiet today doesn't break a streak that ran through yesterday.
    let currentStreak = 0;
    let cursor = activeDates.has(isoDate(end)) ? end : addDays(end, -1);
    while (activeDates.has(isoDate(cursor))) {
      currentStreak++;
      cursor = addDays(cursor, -1);
    }

    let longestStreak = 0;
    let run = 0;
    let previous: string | null = null;
    for (const date of [...activeDates].sort()) {
      run = previous !== null && isoDate(addDays(new Date(`${previous}T00:00:00Z`), 1)) === date ? run + 1 : 1;
      longestStreak = Math.max(longestStreak, run);
      previous = date;
    }

    let activeDays = 0;
    let best: PointsOverTimePoint | null = null;
    for (const point of this.points()) {
      const day = new Date(`${point.date}T00:00:00Z`);
      if (day < start || day > end) continue;
      if (point.activityCount > 0) activeDays++;
      if (point.points > 0 && point.points > (best?.points ?? 0)) best = point;
    }

    return {
      currentStreak,
      longestStreak,
      activeDays,
      bestValue: best ? `${best.points.toLocaleString('en-US')} pts` : '—',
      bestLabel: best ? `Best day · ${shortDate(best.date)}` : 'Best day',
    };
  });
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function isoDate(day: Date): string {
  return day.toISOString().slice(0, 10);
}

function addDays(day: Date, days: number): Date {
  return new Date(day.getTime() + days * DAY_MS);
}

/** The Monday on or before the given UTC day (weeks are Monday-based, like the backend). */
function mondayOf(day: Date): Date {
  return addDays(day, -((day.getUTCDay() + 6) % 7));
}

function levelFor(points: number, maxPoints: number): number {
  if (points <= 0 || maxPoints <= 0) return 0;
  return Math.min(4, Math.max(1, Math.ceil((points / maxPoints) * 4)));
}

function tooltipFor(date: string, bucket: PointsOverTimePoint | undefined): string {
  const day = new Date(`${date}T00:00:00Z`).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  });
  if (!bucket) return `No activity — ${day}`;
  const activities = bucket.activityCount === 1 ? 'activity' : 'activities';
  return `${bucket.points} points · ${bucket.activityCount} ${activities} — ${day}`;
}
