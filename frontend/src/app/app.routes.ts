import { Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/dashboard',
    pathMatch: 'full'
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'portfolios',
    loadChildren: () => import('./features/portfolios/portfolios.routes').then(m => m.PORTFOLIOS_ROUTES),
    canActivate: [AuthGuard]
  },
  {
    path: 'trades',
    loadChildren: () => import('./features/trades/trades.routes').then(m => m.TRADES_ROUTES),
    canActivate: [AuthGuard]
  },
  {
    path: 'analytics',
    loadComponent: () => import('./features/analytics/analytics.component').then(m => m.AnalyticsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'market-data',
    loadComponent: () => import('./features/market-data/market-data.component').then(m => m.MarketDataComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'capital-flows',
    loadComponent: () => import('./features/capital-flows/capital-flows.component').then(m => m.CapitalFlowsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'snapshots',
    loadComponent: () => import('./features/snapshots/snapshots.component').then(m => m.SnapshotsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'risk',
    loadComponent: () => import('./features/risk/risk.component').then(m => m.RiskComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'advanced-analytics',
    loadComponent: () => import('./features/advanced-analytics/advanced-analytics.component').then(m => m.AdvancedAnalyticsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'strategies',
    loadComponent: () => import('./features/strategies/strategies.component').then(m => m.StrategiesComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'journals',
    loadComponent: () => import('./features/journals/journals.component').then(m => m.JournalsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'alerts',
    loadComponent: () => import('./features/alerts/alerts.component').then(m => m.AlertsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'backtesting',
    loadComponent: () => import('./features/backtesting/backtesting.component').then(m => m.BacktestingComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'position-sizing',
    loadComponent: () => import('./features/position-sizing/position-sizing.component').then(m => m.PositionSizingComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'trade-plan',
    loadComponent: () => import('./features/trade-plan/trade-plan.component').then(m => m.TradePlanComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'risk-dashboard',
    loadComponent: () => import('./features/risk-dashboard/risk-dashboard.component').then(m => m.RiskDashboardComponent),
    canActivate: [AuthGuard]
  },
  {
    path: '**',
    loadComponent: () => import('./shared/components/not-found/not-found.component').then(m => m.NotFoundComponent)
  }
];