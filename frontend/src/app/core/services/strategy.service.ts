import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface Strategy {
  id: string;
  name: string;
  description: string;
  entryRules: string;
  exitRules: string;
  riskRules: string;
  timeFrame: string;
  marketCondition: string;
  suggestedSlPercent?: number;
  suggestedRrRatio?: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateStrategyRequest {
  name: string;
  description?: string;
  entryRules?: string;
  exitRules?: string;
  riskRules?: string;
  timeFrame?: string;
  marketCondition?: string;
  suggestedSlPercent?: number;
  suggestedRrRatio?: number;
}

export interface UpdateStrategyRequest {
  name?: string;
  description?: string;
  entryRules?: string;
  exitRules?: string;
  riskRules?: string;
  timeFrame?: string;
  marketCondition?: string;
  isActive?: boolean;
}

export interface StrategyPerformance {
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRate: number;
  totalPnL: number;
  averagePnL: number;
  profitFactor: number;
  averageWin: number;
  averageLoss: number;
  largestWin: number;
  largestLoss: number;
}

export interface StrategyComparison {
  strategyId: string;
  strategyName: string;
  performance: StrategyPerformance;
}

@Injectable({
  providedIn: 'root'
})
export class StrategyService {
  private readonly API_URL = `${environment.apiUrl}/strategies`;

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

  getAll(): Observable<Strategy[]> {
    return this.http.get<Strategy[]>(this.API_URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getById(id: string): Observable<Strategy> {
    return this.http.get<Strategy>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  create(data: CreateStrategyRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  update(id: string, data: UpdateStrategyRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  linkTrade(strategyId: string, tradeId: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${strategyId}/trades/${tradeId}`, {}, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getPerformance(id: string): Observable<StrategyPerformance> {
    return this.http.get<StrategyPerformance>(`${this.API_URL}/${id}/performance`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  compare(strategyIds: string[]): Observable<StrategyComparison[]> {
    return this.http.post<StrategyComparison[]>(`${this.API_URL}/compare`, { strategyIds }, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Strategy API error:', error);
    return throwError(() => error);
  }
}

