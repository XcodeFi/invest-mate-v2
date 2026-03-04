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

  private handleError(error: any): Observable<never> {
    console.error('Analytics API error:', error);
    return throwError(() => error);
  }
}

