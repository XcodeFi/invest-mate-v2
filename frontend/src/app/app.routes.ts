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
    redirectTo: '/analytics',
    pathMatch: 'full'
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
    redirectTo: '/trade-plan',
    pathMatch: 'full'
  },
  {
    path: 'trade-plan',
    loadComponent: () => import('./features/trade-plan/trade-plan.component').then(m => m.TradePlanComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'campaign-analytics',
    loadComponent: () => import('./features/campaign-analytics/campaign-analytics.component').then(m => m.CampaignAnalyticsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'positions',
    loadComponent: () => import('./features/positions/positions.component').then(m => m.PositionsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'risk-dashboard',
    loadComponent: () => import('./features/risk-dashboard/risk-dashboard.component').then(m => m.RiskDashboardComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'monthly-review',
    loadComponent: () => import('./features/monthly-review/monthly-review.component').then(m => m.MonthlyReviewComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'trade-wizard',
    loadComponent: () => import('./features/trade-wizard/trade-wizard.component').then(m => m.TradeWizardComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'trade-replay/:id',
    loadComponent: () => import('./features/trade-replay/trade-replay.component').then(m => m.TradeReplayComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'daily-routine',
    loadComponent: () => import('./features/daily-routine/daily-routine.component').then(m => m.DailyRoutineComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'watchlist',
    loadComponent: () => import('./features/watchlist/watchlist.component').then(m => m.WatchlistComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'symbol-timeline/:symbol',
    loadComponent: () => import('./features/symbol-timeline/symbol-timeline.component').then(m => m.SymbolTimelineComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'symbol-timeline',
    loadComponent: () => import('./features/symbol-timeline/symbol-timeline.component').then(m => m.SymbolTimelineComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'ai-settings',
    loadComponent: () => import('./features/ai-settings/ai-settings.component').then(m => m.AiSettingsComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'help',
    loadComponent: () => import('./features/help/help.component').then(m => m.HelpComponent)
  },
  {
    path: 'changelog',
    loadComponent: () => import('./features/changelog/changelog.component').then(m => m.ChangelogComponent)
  },
  {
    path: '**',
    loadComponent: () => import('./shared/components/not-found/not-found.component').then(m => m.NotFoundComponent)
  }
];