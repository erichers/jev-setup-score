import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./home').then((m) => m.HomePage) },
  { path: 'methodology', loadComponent: () => import('./methodology').then((m) => m.MethodologyPage) },
  { path: 'score/:ticker', loadComponent: () => import('./score').then((m) => m.ScorePage) },
  { path: '**', redirectTo: '' },
];
