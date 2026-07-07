import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';

interface ScoringRule {
  sport: string;
  rule: string;
}

/** Read-only reference of the scoring rules, opened from the dashboard header. */
@Component({
  selector: 'app-scoring-info-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>How points are calculated</h2>
    <mat-dialog-content>
      <p class="intro">Every activity is normalized into a single Points score:</p>
      <table class="rules">
        <tbody>
          @for (rule of rules; track rule.sport) {
            <tr>
              <th scope="row">{{ rule.sport }}</th>
              <td>{{ rule.rule }}</td>
            </tr>
          }
        </tbody>
      </table>
      <p class="note">Fractional points are floored — e.g. 1.55 km of walking = 77 points, and 0:59 of gym = 0.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        min-width: 300px;
      }
      .intro {
        margin: 0 0 12px;
        color: var(--mat-sys-on-surface-variant);
      }
      .rules {
        width: 100%;
        border-collapse: collapse;
      }
      .rules th,
      .rules td {
        padding: 8px 0;
        text-align: left;
        border-bottom: 1px solid color-mix(in srgb, var(--mat-sys-outline) 20%, transparent);
      }
      .rules th {
        font-weight: 600;
      }
      .rules td {
        text-align: right;
        font-variant-numeric: tabular-nums;
        color: var(--mat-sys-on-surface-variant);
      }
      .note {
        margin: 12px 0 0;
        font-size: 0.8rem;
        color: var(--mat-sys-on-surface-variant);
      }
    `,
  ],
})
export class ScoringInfoDialog {
  readonly rules: ScoringRule[] = [
    { sport: 'Running', rule: '1 km = 100 points' },
    { sport: 'Walking', rule: '1 km = 50 points' },
    { sport: 'Cycling', rule: '1 km = 25 points' },
    { sport: 'Swimming', rule: '1 minute = 15 points' },
    { sport: 'Gym', rule: '1 minute = 5 points' },
    { sport: 'Daily Steps', rule: '100 steps = 1 point' },
  ];
}
