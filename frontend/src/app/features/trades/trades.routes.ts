import { Routes } from '@angular/router';

export const TRADES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./trades.component').then(m => m.TradesComponent)
  },
  {
    path: 'create',
    loadComponent: () => import('./trade-create/trade-create.component').then(m => m.TradeCreateComponent)
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./trade-edit/trade-edit.component').then(m => m.TradeEditComponent)
  }
];