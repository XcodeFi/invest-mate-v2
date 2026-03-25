import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

// Risk Profile
export interface RiskProfile {
  id: string;
  portfolioId: string;
  maxPositionSizePercent: number;
  maxSectorExposurePercent: number;
  maxDrawdownAlertPercent: number;
  defaultRiskRewardRatio: number;
  maxPortfolioRiskPercent: number;
  createdAt: string;
  updatedAt: string;
}

export interface SetRiskProfileRequest {
  maxPositionSizePercent?: number;
  maxSectorExposurePercent?: number;
  maxDrawdownAlertPercent?: number;
  defaultRiskRewardRatio?: number;
  maxPortfolioRiskPercent?: number;
}

// Position Risk
export interface PositionRiskItem {
  symbol: string;
  quantity: number;
  currentPrice: number;
  marketValue: number;
  positionSizePercent: number;
  stopLossPrice: number | null;
  targetPrice: number | null;
  riskRewardRatio: number | null;
  riskPerShare: number | null;
  riskAmount: number | null;
  distanceToStopLossPercent: number;
  distanceToTargetPercent: number;
  sector: string | null;
  beta: number | null;
  positionVaR: number | null;
}

export interface PortfolioRiskSummary {
  portfolioId: string;
  totalValue: number;
  positions: PositionRiskItem[];
  maxDrawdown: number;
  valueAtRisk95: number;
  largestPositionPercent: number;
  positionCount: number;
}

// Drawdown
export interface DrawdownPoint {
  date: string;
  value: number;
  drawdownPercent: number;
}

export interface DrawdownResult {
  portfolioId: string;
  maxDrawdownPercent: number;
  currentDrawdownPercent: number;
  peakDate: string | null;
  peakValue: number | null;
  troughDate: string | null;
  troughValue: number | null;
  drawdownSeries: DrawdownPoint[];
}

// Correlation
export interface CorrelationPair {
  symbol1: string;
  symbol2: string;
  correlation: number;
}

export interface CorrelationMatrix {
  portfolioId: string;
  symbols: string[];
  pairs: CorrelationPair[];
}

// Stop-Loss Target
export interface StopLossTargetItem {
  id: string;
  tradeId: string;
  symbol: string;
  entryPrice: number;
  stopLossPrice: number;
  targetPrice: number;
  trailingStopPercent: number | null;
  trailingStopPrice: number | null;
  isStopLossTriggered: boolean;
  isTargetTriggered: boolean;
  triggeredAt: string | null;
  riskRewardRatio: number;
  riskPerShare: number;
  createdAt: string;
}

export interface StopLossTargetsResponse {
  portfolioId: string;
  items: StopLossTargetItem[];
}

export interface SetStopLossTargetRequest {
  tradeId: string;
  portfolioId: string;
  symbol: string;
  entryPrice: number;
  stopLossPrice: number;
  targetPrice: number;
  trailingStopPercent?: number;
}

// Portfolio Optimization
export interface ConcentrationAlert {
  symbol: string;
  positionPercent: number;
  limit: number;
  severity: 'warning' | 'danger';
}

export interface SectorExposure {
  sector: string;
  symbols: string[];
  totalValue: number;
  exposurePercent: number;
  limit: number;
  isOverweight: boolean;
}

export interface CorrelationWarning {
  symbol1: string;
  symbol2: string;
  correlation: number;
  riskLevel: 'high' | 'medium';
}

export interface PortfolioOptimizationResult {
  portfolioId: string;
  totalValue: number;
  diversificationScore: number;
  concentrationAlerts: ConcentrationAlert[];
  sectorExposures: SectorExposure[];
  correlationWarnings: CorrelationWarning[];
  recommendations: string[];
}

// Trailing Stop Alerts
export interface TrailingStopAlert {
  symbol: string;
  tradeId: string;
  entryPrice: number;
  currentPrice: number;
  trailingStopPercent: number;
  trailingStopPrice: number;
  distancePercent: number;
  severity: 'danger' | 'warning' | 'safe';
  shouldUpdatePrice: boolean;
  newTrailingStopPrice: number | null;
}

export interface TrailingStopAlertsResult {
  portfolioId: string;
  alerts: TrailingStopAlert[];
  totalActiveTrailingStops: number;
  alertCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class RiskService {
  private readonly API_URL = `${environment.apiUrl}/risk`;

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {}

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  getRiskProfile(portfolioId: string): Observable<RiskProfile> {
    return this.http.get<RiskProfile>(`${this.API_URL}/portfolio/${portfolioId}/profile`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  setRiskProfile(portfolioId: string, data: SetRiskProfileRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.API_URL}/portfolio/${portfolioId}/profile`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getPortfolioRiskSummary(portfolioId: string): Observable<PortfolioRiskSummary> {
    return this.http.get<PortfolioRiskSummary>(`${this.API_URL}/portfolio/${portfolioId}/summary`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getDrawdown(portfolioId: string): Observable<DrawdownResult> {
    return this.http.get<DrawdownResult>(`${this.API_URL}/portfolio/${portfolioId}/drawdown`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getCorrelation(portfolioId: string): Observable<CorrelationMatrix> {
    return this.http.get<CorrelationMatrix>(`${this.API_URL}/portfolio/${portfolioId}/correlation`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getStopLossTargets(portfolioId: string): Observable<StopLossTargetsResponse> {
    return this.http.get<StopLossTargetsResponse>(`${this.API_URL}/portfolio/${portfolioId}/stop-loss`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  setStopLossTarget(data: SetStopLossTargetRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.API_URL}/stop-loss`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getPortfolioOptimization(portfolioId: string): Observable<PortfolioOptimizationResult> {
    return this.http.get<PortfolioOptimizationResult>(`${this.API_URL}/portfolio/${portfolioId}/optimization`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTrailingStopAlerts(portfolioId: string): Observable<TrailingStopAlertsResult> {
    return this.http.get<TrailingStopAlertsResult>(`${this.API_URL}/portfolio/${portfolioId}/trailing-stop-alerts`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Risk API error:', error);
    return throwError(() => error);
  }
}

