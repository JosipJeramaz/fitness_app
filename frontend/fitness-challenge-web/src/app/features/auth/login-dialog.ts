import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { RegistrationDialog } from '../registration/registration-dialog';

type LoginMode = 'credentials' | 'name';

/**
 * Error styling by touched state only — the default also flags fields once the form was submitted,
 * which would leave the other mode's untouched fields red after a failed submit + mode switch.
 */
class TouchedErrorStateMatcher implements ErrorStateMatcher {
  isErrorState(control: FormControl | null): boolean {
    return !!control && control.invalid && control.touched;
  }
}

@Component({
  selector: 'app-login-dialog',
  providers: [{ provide: ErrorStateMatcher, useClass: TouchedErrorStateMatcher }],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatButtonToggleModule,
  ],
  template: `
    <h2 mat-dialog-title>Log in</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="form-body">
        <mat-button-toggle-group
          class="mode-toggle"
          [value]="mode()"
          (change)="setMode($event.value)"
          hideSingleSelectionIndicator
          aria-label="Login method"
        >
          <mat-button-toggle value="credentials">Username</mat-button-toggle>
          <mat-button-toggle value="name">Name only</mat-button-toggle>
        </mat-button-toggle-group>

        @if (mode() === 'credentials') {
          <mat-form-field appearance="outline">
            <mat-label>Username</mat-label>
            <input matInput formControlName="userName" autocomplete="username" />
            @if (form.controls.userName.touched && form.controls.userName.invalid) {
              <mat-error>Username is required.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="current-password" />
            @if (form.controls.password.touched && form.controls.password.invalid) {
              <mat-error>Password is required.</mat-error>
            }
          </mat-form-field>
        } @else {
          <mat-form-field appearance="outline">
            <mat-label>First name</mat-label>
            <input matInput formControlName="firstName" autocomplete="given-name" />
            @if (form.controls.firstName.touched && form.controls.firstName.invalid) {
              <mat-error>First name is required.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Last name</mat-label>
            <input matInput formControlName="lastName" autocomplete="family-name" />
            @if (form.controls.lastName.touched && form.controls.lastName.invalid) {
              <mat-error>Last name is required.</mat-error>
            }
          </mat-form-field>

          <p class="mode-hint">
            ⚠️ Not recommended: anyone who knows your name can log in as you. This only works for
            accounts without a password — register a username &amp; password to protect yours.
          </p>
        }

        @if (error()) {
          <p class="form-error">{{ error() }}</p>
        }
      </mat-dialog-content>

      <mat-dialog-actions class="actions">
        <button mat-button type="button" (click)="openRegistration()">No account? Register</button>
        <span class="actions-spacer"></span>
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Logging in…' : 'Log in' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [
    `
      .form-body {
        display: flex;
        flex-direction: column;
        gap: 8px;
        padding-top: 8px;
        min-width: 320px;
      }
      .mode-toggle {
        align-self: center;
        margin-bottom: 4px;
      }
      .mode-hint {
        margin: 0;
        font-size: 0.8rem;
        color: var(--mat-sys-on-surface-variant);
      }
      .actions-spacer {
        flex: 1 1 auto;
      }
      .form-error {
        color: var(--mat-sys-error);
        margin: 0;
      }
    `,
  ],
})
export class LoginDialog {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly dialogRef = inject(MatDialogRef<LoginDialog>);
  private readonly router = inject(Router);

  readonly mode = signal<LoginMode>('credentials');
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    userName: ['', Validators.required],
    password: ['', Validators.required],
    firstName: [{ value: '', disabled: true }, Validators.required],
    lastName: [{ value: '', disabled: true }, Validators.required],
  });

  /** Only the active mode's controls stay enabled, so `form.invalid` tracks just the visible fields. */
  setMode(mode: LoginMode): void {
    this.mode.set(mode);
    this.error.set(null);

    const { userName, password, firstName, lastName } = this.form.controls;
    const credentials = mode === 'credentials';
    for (const control of [userName, password]) (credentials ? control.enable : control.disable).call(control);
    for (const control of [firstName, lastName]) (credentials ? control.disable : control.enable).call(control);

    // A failed submit in one mode must not pre-flag the other mode's untouched fields as errors.
    this.form.markAsUntouched();
  }

  /** Hand off to registration — the register dialog refreshes the user list and signs the user in itself. */
  openRegistration(): void {
    this.dialogRef.close();
    this.dialog
      .open(RegistrationDialog, { width: '420px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((result: string | boolean | undefined) => {
        // A userId (non-empty string) means auto-login succeeded; `true`, `''` (bare Cancel) or
        // undefined mean "no navigation".
        if (result && typeof result === 'string') {
          this.router.navigate(['/dashboard', result]);
        }
      });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const { userName, password, firstName, lastName } = this.form.getRawValue();
    const login$: Observable<{ userId: string }> =
      this.mode() === 'credentials'
        ? this.authService.login(userName, password)
        : this.authService.loginByName(firstName, lastName);

    login$.subscribe({
      next: (response) => this.dialogRef.close(response.userId),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  private describeError(err: HttpErrorResponse): string {
    if (err.status === 401) {
      return err.error?.detail ?? 'Invalid username or password.';
    }
    return err.error?.title ?? 'Login failed. Please try again.';
  }
}
