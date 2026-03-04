import { Routes } from '@angular/router';

export const PORTFOLIOS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./portfolios.component').then(m => m.PortfoliosComponent)
  },
  {
    path: 'create',
    loadComponent: () => import('./portfolio-create/portfolio-create.component').then(m => m.PortfolioCreateComponent)
  },
  {
    path: ':id',
    loadComponent: () => import('./portfolio-detail/portfolio-detail.component').then(m => m.PortfolioDetailComponent)
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./portfolio-edit/portfolio-edit.component').then(m => m.PortfolioEditComponent)
  },
  {
    path: ':id/trades',
    loadComponent: () => import('./portfolio-trades/portfolio-trades.component').then(m => m.PortfolioTradesComponent)
  },
  {
    path: ':id/analytics',
    loadComponent: () => import('./portfolio-analytics/portfolio-analytics.component').then(m => m.PortfolioAnalyticsComponent)
  }
];