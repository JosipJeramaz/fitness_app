import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Trend } from '../../core/models/models';

/**
 * Compact up/down/same rank-trend badge used in the leaderboard. A legacy "new" value
 * (no longer produced by the API) folds into "—" as a safety net.
 */
@Component({
  selector: 'app-trend-indicator',
  imports: [MatIconModule],
  template: `
    <span class="trend trend--{{ displayTrend() }}" [attr.aria-label]="ariaLabel()">
      <mat-icon>{{ icon() }}</mat-icon>
      <span class="trend__label">{{ label() }}</span>
    </span>
  `,
  styles: [
    `
      .trend {
        display: inline-flex;
        align-items: center;
        gap: 2px;
        font-weight: 600;
        font-variant-numeric: tabular-nums;
      }
      .trend mat-icon {
        font-size: 20px;
        width: 20px;
        height: 20px;
      }
      .trend--up {
        color: #1a8f3c;
      }
      .trend--down {
        color: #c62828;
      }
      .trend--same {
        color: var(--mat-sys-outline);
      }
    `,
  ],
})
export class TrendIndicator {
  readonly trend = input.required<Trend>();
  readonly delta = input<number | null>(null);

  /** "new" collapses into "same" for display purposes. */
  readonly displayTrend = computed(() => (this.trend() === 'new' ? 'same' : this.trend()));

  readonly icon = computed(() => {
    switch (this.displayTrend()) {
      case 'up':
        return 'arrow_upward';
      case 'down':
        return 'arrow_downward';
      default:
        return 'remove';
    }
  });

  readonly label = computed(() => {
    if (this.displayTrend() === 'same') return '—';
    const delta = this.delta();
    return delta != null ? Math.abs(delta).toString() : '';
  });

  readonly ariaLabel = computed(() => {
    const trend = this.displayTrend();
    if (trend === 'same') return 'No change';
    return `Moved ${trend} ${Math.abs(this.delta() ?? 0)} places`;
  });
}
