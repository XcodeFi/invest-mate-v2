import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface BacktestSummary {
  id: string;
  name: string;
  strategyId: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  status: string;
  createdAt: string;
  hasResult: boolean;
}

export interface EquityCurvePoint {
  date: string;
  portfolioValue: number;
  dailyReturn: number;
  cumulativeReturn: number;
}

export interface SimulatedTrade {
  symbol: string;
  type: string;
  entryPrice: number;
  exitPrice: number;
  quantity: number;
  entryDate: string;
  exitDate: string;
  pnL: number;
  returnPercent: number;
}

export interface BacktestResult {
  finalValue: number;
  totalReturn: number;
  cagr: number;
  sharpeRatio: number;
  maxDrawdown: number;
  winRate: number;
  profitFactor: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  equityCurve: EquityCurvePoint[];
}

export interface BacktestDetail {
  id: string;
  name: string;
  strategyId: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  status: string;
  result: BacktestResult | null;
  simulatedTrades: SimulatedTrade[];
  errorMessage: string | null;
  createdAt: string;
}

export interface RunBacktestRequest {
  strategyId: string;
  name: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
}

@Injectable({
  providedIn: 'root'
})
export class BacktestService {
  private readonly API_URL = `${environment.apiUrl}/backtests`;

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

  getAll(): Observable<BacktestSummary[]> {
    return this.http.get<BacktestSummary[]>(this.API_URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getById(id: string): Observable<BacktestDetail> {
    return this.http.get<BacktestDetail>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  run(data: RunBacktestRequest): Observable<{ id: string; message: string }> {
    return this.http.post<{ id: string; message: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getEquityCurve(id: string): Observable<{ status: string; equityCurve: EquityCurvePoint[] }> {
    return this.http.get<{ status: string; equityCurve: EquityCurvePoint[] }>(
      `${this.API_URL}/${id}/equity-curve`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTrades(id: string): Observable<SimulatedTrade[]> {
    return this.http.get<SimulatedTrade[]>(`${this.API_URL}/${id}/trades`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Backtest API error:', error);
    return throwError(() => error);
  }
}
