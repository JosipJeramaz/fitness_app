import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../core/services/auth.service';
import { UserService } from '../../core/services/user.service';

@Component({
  selector: 'app-registration-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Register a user</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="form-body">
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

        <mat-form-field appearance="outline">
          <mat-label>Username (optional — protects your account)</mat-label>
          <input matInput formControlName="userName" autocomplete="username" />
          @if (form.controls.userName.touched && form.controls.userName.hasError('pattern')) {
            <mat-error>3–50 characters: letters, digits, '.', '_' or '-'.</mat-error>
          }
          @if (form.controls.userName.touched && form.controls.userName.hasError('required')) {
            <mat-error>Username is required when a password is set.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Password (optional — protects your account)</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="new-password" />
          @if (form.controls.password.touched && form.controls.password.hasError('required')) {
            <mat-error>Password is required when a username is set.</mat-error>
          }
        </mat-form-field>

        @if (error()) {
          <p class="form-error">{{ error() }}</p>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Saving…' : 'Register' }}
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
      .form-error {
        color: var(--mat-sys-error);
        margin: 0;
      }
    `,
  ],
})
export class RegistrationDialog {
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialogRef = inject(MatDialogRef<RegistrationDialog>);

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    userName: ['', Validators.pattern(/^[a-zA-Z0-9._-]{3,50}$/)],
    password: [''],
  });

  constructor() {
    // Username and password enable login together — requiring one marks the other required.
    this.form.controls.userName.valueChanges.subscribe(() => this.syncCredentialRequirements());
    this.form.controls.password.valueChanges.subscribe(() => this.syncCredentialRequirements());
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const { firstName, lastName, userName, password } = this.form.getRawValue();
    const request =
      userName && password ? { firstName, lastName, userName, password } : { firstName, lastName };

    this.userService.register(request).subscribe({
      next: () => {
        // Refresh the shared user list here so every opener (toolbar, login dialog) sees the new user.
        this.userService.loadUsers().subscribe();
        this.loginAfterRegistration(firstName, lastName, userName, password);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  /**
   * The user just typed their details — asking them to retype in a login form is pure friction, so
   * they're signed in immediately (credentials mode, or the name fallback for credential-less users).
   * Closes with the userId so the opener can navigate to their dashboard.
   */
  private loginAfterRegistration(firstName: string, lastName: string, userName: string, password: string): void {
    const login$ =
      userName && password ? this.authService.login(userName, password) : this.authService.loginByName(firstName, lastName);

    login$.subscribe({
      next: (response) => {
        this.snackBar.open(`Welcome, ${firstName}! You're registered and logged in.`, 'Close', { duration: 4000 });
        this.dialogRef.close(response.userId);
      },
      error: () => {
        // Registration itself succeeded — worst case the user logs in manually.
        this.snackBar.open('Registration successful — please log in.', 'Close', { duration: 4000 });
        this.dialogRef.close(true);
      },
    });
  }

  private syncCredentialRequirements(): void {
    const { userName, password } = this.form.getRawValue();

    const userNameValidators = [Validators.pattern(/^[a-zA-Z0-9._-]{3,50}$/)];
    if (password) userNameValidators.push(Validators.required);
    this.form.controls.userName.setValidators(userNameValidators);
    this.form.controls.password.setValidators(userName ? [Validators.required] : []);

    this.form.controls.userName.updateValueAndValidity({ emitEvent: false });
    this.form.controls.password.updateValueAndValidity({ emitEvent: false });
  }

  private describeError(err: HttpErrorResponse): string {
    if (err.status === 409) return err.error?.detail ?? 'A user with those details already exists.';
    return err.error?.title ?? 'Registration failed. Please try again.';
  }
}
