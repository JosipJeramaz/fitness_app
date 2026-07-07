import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'leaderboard' },
  {
    path: 'leaderboard',
    title: 'Leaderboard · Fitness Challenge',
    loadComponent: () => import('./features/leaderboard/leaderboard').then((m) => m.Leaderboard),
  },
  {
    path: 'dashboard/:userId',
    title: 'Dashboard · Fitness Challenge',
    loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
  },
  // Logging an activity is a dialog (toolbar button), not a route — old /log URLs fall through here.
  { path: '**', redirectTo: 'leaderboard' },
];
