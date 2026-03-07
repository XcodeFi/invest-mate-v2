import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

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

export interface PortfolioRiskSummary {
  portfolioId: string;
  totalValue: number;
  maxDrawdown: number;
  valueAtRisk95: number;
  largestPositionPercent: number;
  positionCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {
  private readonly ANALYTICS_URL = `${environment.apiUrl}/analytics`;
  private readonly RISK_URL = `${environment.apiUrl}/risk`;

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
    return this.http.get<PerformanceSummary>(
      `${this.ANALYTICS_URL}/portfolio/${portfolioId}/performance`,
      { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  getRiskSummary(portfolioId: string): Observable<PortfolioRiskSummary> {
    return this.http.get<PortfolioRiskSummary>(
      `${this.RISK_URL}/portfolio/${portfolioId}/summary`,
      { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Analytics API error:', error);
    return throwError(() => error);
  }
}
