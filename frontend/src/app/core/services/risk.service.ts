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
  maxDailyTrades?: number;
  dailyLossLimitPercent?: number;
  createdAt: string;
  updatedAt: string;
}

export interface SetRiskProfileRequest {
  maxPositionSizePercent?: number;
  maxSectorExposurePercent?: number;
  maxDrawdownAlertPercent?: number;
  defaultRiskRewardRatio?: number;
  maxPortfolioRiskPercent?: number;
  maxDailyTrades?: number;
  dailyLossLimitPercent?: number;
}

// Stress Test
export interface StressTestPositionItem {
  symbol: string;
  marketValue: number;
  beta: number;
  impact: number;
  valueAfter: number;
}

export interface StressTestResult {
  portfolioId: string;
  marketChangePercent: number;
  positions: StressTestPositionItem[];
  totalImpact: number;
  totalImpactPercent: number;
  totalValueBefore: number;
  totalValueAfter: number;
}

// Risk Budget
export interface RiskBudgetStatus {
  tradesToday: number;
  maxDailyTrades?: number;
  dailyPnl: number;
  dailyLossLimitPercent?: number;
  isLocked: boolean;
  lockReason?: string;
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

// Tỷ trọng ngành quanh một lệnh dự kiến. sector/currentPercent/projectedPercent đều nullable:
// null nghĩa là "chưa tính được" (không tra được ngành, hoặc tổng giá trị danh mục ≤ 0) — khác hẳn
// 0 nghĩa là "chưa giữ gì ngành này". UI phải hiện "n/a" cho null, không hiện 0%.
export interface SectorExposureForPlan {
  symbol: string;
  sector: string | null;
  currentPercent: number | null;
  projectedPercent: number | null;
  limitPercent: number;
  sameSectorSymbols: string[];
}

// Trần khối lượng theo ngân sách biến động (ADR-0014). Mọi trường phần trăm nullable có chủ ý:
// null nghĩa là "chưa tính được", KHÔNG phải 0.
export type VolatilityDataQuality = 'Full' | 'Partial' | 'Insufficient';

export interface VolatilitySizingResult {
  symbol: string;
  currentVolatilityPercent: number | null;
  projectedVolatilityPercent: number | null;
  budgetVolatilityPercent: number;
  sourceMaxDrawdownPercent: number;
  correlationWithPortfolio: number | null;
  marginalRiskContributionPercent: number | null;
  capitalWeightPercent: number | null;
  // null + isUnconstrainedByVolatility=false → không tính được.
  // null + isUnconstrainedByVolatility=true  → không bị ràng buộc. Hai ca khác hẳn nhau.
  maxQuantityWithinBudget: number | null;
  isUnconstrainedByVolatility: boolean;
  portfolioAlreadyOverBudget: boolean;
  dataQuality: VolatilityDataQuality;
  missingSymbols: string[];
  adjustedSymbols: string[];
  /** Mã mà việc LẤY lịch sử hỏng — khác hẳn mã thật sự chưa đủ lịch sử. Xem `missingSymbols`. */
  fetchFailedSymbols: string[];
  observationCount: number;
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

// Position Sizing
export interface PositionSizingRequest {
  accountBalance: number;
  entryPrice: number;
  stopLoss: number;
  riskPercent: number;
  maxPositionPercent: number;
  atr?: number;
  atrMultiplier: number;
  winRate?: number;
  averageWin?: number;
  averageLoss?: number;
  atrPercent?: number;
}

export interface SizingModelResult {
  model: string;
  modelVi: string;
  shares: number;
  positionValue: number;
  positionPercent: number;
  riskAmount: number;
  withinLimit: boolean;
  note?: string;
}

export interface PositionSizingResult {
  models: SizingModelResult[];
  recommendedModel: string;
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

  // symbol và addValue là tham số bắt buộc phía server: thay giá trị thiếu bằng 0 sẽ trả về một
  // con số trông như thật (tỷ trọng "sau lệnh" bằng tỷ trọng hiện tại).
  getSectorExposureForPlan(portfolioId: string, symbol: string, addValue: number): Observable<SectorExposureForPlan> {
    const params = `?symbol=${encodeURIComponent(symbol)}&addValue=${addValue}`;
    return this.http.get<SectorExposureForPlan>(
      `${this.API_URL}/portfolio/${portfolioId}/sector-exposure${params}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  // Cả ba tham số bắt buộc phía server. quantity=0 sẽ cho "biến động sau lệnh" bằng đúng biến động
  // hiện tại — một con số trông như thật.
  getVolatilitySizingForPlan(
    portfolioId: string, symbol: string, entryPrice: number, quantity: number
  ): Observable<VolatilitySizingResult> {
    const params = `?symbol=${encodeURIComponent(symbol)}&entryPrice=${entryPrice}&quantity=${quantity}`;
    return this.http.get<VolatilitySizingResult>(
      `${this.API_URL}/portfolio/${portfolioId}/volatility-sizing${params}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTrailingStopAlerts(portfolioId: string): Observable<TrailingStopAlertsResult> {
    return this.http.get<TrailingStopAlertsResult>(`${this.API_URL}/portfolio/${portfolioId}/trailing-stop-alerts`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  stressTest(portfolioId: string, marketChangePercent: number): Observable<StressTestResult> {
    return this.http.post<StressTestResult>(`${this.API_URL}/portfolio/${portfolioId}/stress-test`,
      { marketChangePercent }, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getRiskBudget(portfolioId: string): Observable<RiskBudgetStatus> {
    return this.http.get<RiskBudgetStatus>(`${this.API_URL}/portfolio/${portfolioId}/budget`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  calculatePositionSizing(request: PositionSizingRequest): Observable<PositionSizingResult> {
    return this.http.post<PositionSizingResult>(`${this.API_URL}/position-sizing`, request, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Risk API error:', error);
    return throwError(() => error);
  }
}

