import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from './core/services/auth.service';
import { UserService } from './core/services/user.service';
import { ActivityEntryDialog } from './features/activity-entry/activity-entry-dialog';
import { LoginDialog } from './features/auth/login-dialog';
import { RegistrationDialog } from './features/registration/registration-dialog';
import { ConfirmDialog, ConfirmDialogData } from './shared/confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTooltipModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly users = this.userService.users;

  /** The signed-in user's list entry (null while logged out or before the user list loads). */
  readonly currentUser = computed(
    () => this.users().find((user) => user.id === this.authService.userId()) ?? null,
  );

  ngOnInit(): void {
    this.userService.loadUsers().subscribe();
  }

  openLogActivity(): void {
    // Success feedback (snackbar) and dashboard refresh (ActivityService.activityLogged)
    // are handled by the dialog itself — nothing to do with the result here.
    this.dialog.open(ActivityEntryDialog, { width: '480px', autoFocus: 'first-tabbable' });
  }

  openRegistration(): void {
    // The dialog refreshes the user list and signs the new user in itself; a userId result means success.
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

  openLogin(): void {
    this.dialog
      .open(LoginDialog, { width: '420px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((userId: string | undefined) => {
        if (userId) {
          this.router.navigate(['/dashboard', userId]);
        }
      });
  }

  logout(): void {
    const data: ConfirmDialogData = {
      title: 'Log out?',
      message: 'You will need to log in again to record activities or delete your entries.',
      confirmLabel: 'Log out',
    };
    this.dialog
      .open(ConfirmDialog, { width: '420px', data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.authService.logout();
          this.router.navigate(['/leaderboard']);
        }
      });
  }
}
