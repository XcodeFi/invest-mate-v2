import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface PerformanceSummary {
  portfolioId: string;
  cagr: number;
  sharpeRatio: number;
  sortinoRatio: number;
  winRate: number;
  profitFactor: number;
  expectancy: number;
  maxDrawdown: number;
  totalReturn: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  averageWin: number;
  averageLoss: number;
  grossProfit: number;
  grossLoss: number;
}

export interface EquityCurvePoint {
  date: string;
  portfolioValue: number;
  dailyReturn: number;
  cumulativeReturn: number;
}

export interface EquityCurveData {
  portfolioId: string;
  points: EquityCurvePoint[];
}

export interface MonthlyReturnItem {
  year: number;
  month: number;
  returnPercent: number;
}

export interface MonthlyReturnsData {
  portfolioId: string;
  returns: MonthlyReturnItem[];
  years: number[];
}

export type SavingsRateSource = 'user-savings-avg' | 'fallback-5' | 'manual';

export interface SavingsCurvePoint {
  date: string;  // ISO
  value: number;
}

export interface SavingsFlowEvent {
  date: string;  // ISO
  signedAmount: number;
}

export interface SavingsComparisonDto {
  actualValue: number;
  hypotheticalValue: number;
  opportunityCost: number;
  /** Null khi hypothetical ≤ 0 (withdraw-heavy portfolio — percent undefined). */
  opportunityCostPercent: number | null;
  usedRate: number;
  rateSource: SavingsRateSource;
  savingsAccountsCounted: number;
  savingsAccountsTotal: number;
  actualCurve: SavingsCurvePoint[];
  flows: SavingsFlowEvent[];
  cagrActual: number | null;
  alphaAnnualized: number | null;
  periodReturnDiff: number | null;
  asOf: string;
  firstFlowDate: string | null;
}

export interface BankRateEntry {
  termMonths: number;
  ratePercent: number;
  bankName: string;
}

export interface BankRateSnapshot {
  topByTerm: Record<number, BankRateEntry>;
  sourceTimestamp: string | null;
  fetchedAt: string;
}

export interface HouseholdReturnSummary {
  userId: string;
  portfolioCount: number;
  totalValue: number;
  timeWeightedReturn: number;
  cagr: number;
  firstSnapshotDate: string | null;
  lastSnapshotDate: string | null;
  daysSpanned: number;
  /** True when daysSpanned ≥ 365 — CAGR not an extreme extrapolation. */
  isStable: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AdvancedAnalyticsService {
  private readonly API_URL = `${environment.apiUrl}/analytics`;

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

  getPerformance(portfolioId: string): Observable<PerformanceSummary> {
    return this.http.get<PerformanceSummary>(`${this.API_URL}/portfolio/${portfolioId}/performance`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getEquityCurve(portfolioId: string): Observable<EquityCurveData> {
    return this.http.get<EquityCurveData>(`${this.API_URL}/portfolio/${portfolioId}/equity-curve`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getMonthlyReturns(portfolioId: string): Observable<MonthlyReturnsData> {
    return this.http.get<MonthlyReturnsData>(`${this.API_URL}/portfolio/${portfolioId}/monthly-returns`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  /**
   * So sánh hiệu suất danh mục với tiết kiệm. Rate null → backend dùng weighted avg / fallback 5%.
   */
  getSavingsComparison(portfolioId: string, savingsRate?: number, asOf?: Date): Observable<SavingsComparisonDto> {
    let url = `${this.API_URL}/portfolio/${portfolioId}/vs-savings`;
    const params: string[] = [];
    if (savingsRate != null) params.push(`savingsRate=${savingsRate}`);
    if (asOf != null) params.push(`asOf=${encodeURIComponent(asOf.toISOString())}`);
    if (params.length) url += '?' + params.join('&');
    return this.http.get<SavingsComparisonDto>(url, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getBankRates(): Observable<BankRateSnapshot> {
    return this.http.get<BankRateSnapshot>(`${this.API_URL}/bank-rates`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getHouseholdPerformance(): Observable<HouseholdReturnSummary> {
    return this.http.get<HouseholdReturnSummary>(`${this.API_URL}/household/performance`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Analytics API error:', error);
    return throwError(() => error);
  }
}

