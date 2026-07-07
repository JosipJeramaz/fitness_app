import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ACTIVITY_KINDS, ActivityKind, IngestActivityRequest, Sport } from '../../core/models/models';
import { ActivityService } from '../../core/services/activity.service';
import { AuthService } from '../../core/services/auth.service';
import { UserService } from '../../core/services/user.service';
import { LoginDialog } from '../auth/login-dialog';

@Component({
  selector: 'app-activity-entry-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './activity-entry-dialog.html',
  styleUrl: './activity-entry-dialog.scss',
})
export class ActivityEntryDialog implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly activityService = inject(ActivityService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly dialogRef = inject(MatDialogRef<ActivityEntryDialog>);
  private readonly snackBar = inject(MatSnackBar);

  private readonly durationPattern = /^\d{1,}:[0-5]\d$/;

  readonly users = this.userService.users;
  readonly kinds = ACTIVITY_KINDS;
  readonly submitting = signal(false);
  readonly selectedMetric = signal<'distance' | 'duration' | 'steps'>('distance');
  /** Smallest value of the selected sport worth >= 1 point (km / total seconds / steps), mirrored from the backend. */
  readonly selectedMin = signal(0.01);
  /** Per-entry cap of the selected sport (km / total seconds / steps), mirrored from the backend. */
  readonly selectedMax = signal(100);

  /** Activities are self-only: the signed-in user is the only possible athlete. */
  readonly currentUser = computed(
    () => this.users().find((user) => user.id === this.authService.userId()) ?? null,
  );

  /** Oldest datetime the backend accepts (one year back) — also feeds the input's native `min`. */
  readonly minDatetimeLocal = this.oneYearAgoLocal();
  /** Newest datetime worth offering (now) — feeds the input's native `max`. */
  readonly maxDatetimeLocal = this.nowLocal();

  readonly form = this.fb.nonNullable.group({
    kind: ['running' as ActivityKind, Validators.required],
    datetime: [this.nowLocal(), [Validators.required, this.minDatetime(), this.maxDatetime()]],
    distance: [null as number | null],
    duration: [''],
    steps: [null as number | null],
  });

  constructor() {
    // Reconfigure the metric field whenever the sport changes.
    this.form.controls.kind.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((kind) => this.applyKind(kind));
  }

  ngOnInit(): void {
    this.userService.loadUsers().subscribe();
    this.applyKind(this.form.controls.kind.value);
  }

  /** Swaps this dialog for the login one; a successful login brings the user straight back here. */
  openLogin(): void {
    this.dialogRef.close();
    this.dialog
      .open(LoginDialog, { width: '420px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((userId: string | undefined) => {
        if (userId) {
          this.dialog.open(ActivityEntryDialog, { width: '480px', autoFocus: 'first-tabbable' });
        }
      });
  }

  submit(): void {
    const userId = this.authService.userId();
    if (this.form.invalid || !userId) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: IngestActivityRequest = {
      userId,
      datetime: new Date(value.datetime).toISOString(),
    };

    if (value.kind === 'dailySteps') {
      request.steps = Number(value.steps);
    } else {
      request.sport = value.kind as Sport;
      if (this.selectedMetric() === 'distance') request.distance = Number(value.distance);
      else request.duration = value.duration;
    }

    this.submitting.set(true);
    this.activityService.ingest(request).subscribe({
      next: (response) => {
        this.snackBar.open(`Logged ${response.activityType} · ${response.points} points 🎉`, 'Close', { duration: 4000 });
        this.dialogRef.close(response);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.snackBar.open(this.describeError(err), 'Close', { duration: 6000 });
      },
    });
  }

  /** Enables only the metric control the chosen sport needs and wires up its validators. */
  private applyKind(kind: ActivityKind): void {
    const option = this.kinds.find((k) => k.value === kind)!;
    this.selectedMetric.set(option.metric);
    this.selectedMin.set(option.min);
    this.selectedMax.set(option.max);

    const { distance, duration, steps } = this.form.controls;
    for (const control of [distance, duration, steps]) {
      control.clearValidators();
      control.disable({ emitEvent: false });
    }
    distance.reset(null, { emitEvent: false });
    duration.reset('', { emitEvent: false });
    steps.reset(null, { emitEvent: false });

    if (option.metric === 'distance') {
      distance.enable({ emitEvent: false });
      distance.setValidators([Validators.required, Validators.min(option.min), Validators.max(option.max)]);
    } else if (option.metric === 'duration') {
      duration.enable({ emitEvent: false });
      duration.setValidators([
        Validators.required,
        Validators.pattern(this.durationPattern),
        this.minDuration(option.min),
        this.maxDuration(option.max),
      ]);
    } else {
      steps.enable({ emitEvent: false });
      steps.setValidators([Validators.required, Validators.min(option.min), Validators.max(option.max)]);
    }

    distance.updateValueAndValidity({ emitEvent: false });
    duration.updateValueAndValidity({ emitEvent: false });
    steps.updateValueAndValidity({ emitEvent: false });
  }

  /** Requires at least the sport's minimum duration (worth >= 1 point). Format stays owned by the pattern validator. */
  private minDuration(minSeconds: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value as string;
      if (!value || !this.durationPattern.test(value)) return null;
      const [minutes, seconds] = value.split(':').map(Number);
      return minutes * 60 + seconds < minSeconds ? { minDuration: { min: minSeconds } } : null;
    };
  }

  /** Caps mm:ss duration at the sport's max total seconds. Format errors stay owned by the pattern validator. */
  private maxDuration(maxSeconds: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value as string;
      if (!value || !this.durationPattern.test(value)) return null;
      const [minutes, seconds] = value.split(':').map(Number);
      return minutes * 60 + seconds > maxSeconds ? { maxDuration: { max: maxSeconds } } : null;
    };
  }

  /** Mirrors the backend rule: 'datetime' must not be more than one year in the past. */
  private minDatetime(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value as string;
      if (!value) return null; // required validator owns the empty case
      const oneYearAgo = new Date();
      oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1);
      return new Date(value) < oneYearAgo ? { minDatetime: true } : null;
    };
  }

  /** Mirrors the backend rule: 'datetime' must not be in the future (same 5-min skew tolerance). */
  private maxDatetime(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value as string;
      if (!value) return null;
      const graceLimit = new Date(Date.now() + 5 * 60_000);
      return new Date(value) > graceLimit ? { maxDatetime: true } : null;
    };
  }

  private nowLocal(): string {
    return this.toLocalInputValue(new Date());
  }

  private oneYearAgoLocal(): string {
    const day = new Date();
    day.setFullYear(day.getFullYear() - 1);
    return this.toLocalInputValue(day);
  }

  private toLocalInputValue(day: Date): string {
    day.setSeconds(0, 0);
    const pad = (value: number) => value.toString().padStart(2, '0');
    return `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}T${pad(day.getHours())}:${pad(day.getMinutes())}`;
  }

  private describeError(err: HttpErrorResponse): string {
    if (err.status === 401) return 'Your session has expired — please log in again.';
    if (err.status === 403) return err.error?.title ?? 'You can only log activities for yourself.';
    const errors = err.error?.errors as Record<string, string[]> | undefined;
    if (errors) {
      const first = Object.values(errors)[0];
      if (first?.length) return first[0];
    }
    return err.error?.title ?? 'Could not log the activity.';
  }
}
