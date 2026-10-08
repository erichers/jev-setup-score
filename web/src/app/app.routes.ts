import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./home').then((m) => m.HomePage) },
  { path: 'score/:ticker', loadComponent: () => import('./score').then((m) => m.ScorePage) },
  { path: '**', redirectTo: '' },
];
