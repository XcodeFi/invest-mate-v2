import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface CapitalFlowItem {
  id: string;
  type: string;
  amount: number;
  currency: string;
  note: string | null;
  flowDate: string;
  createdAt: string;
  isSeedDeposit: boolean;
}

export interface CapitalFlowHistory {
  portfolioId: string;
  flows: CapitalFlowItem[];
  totalDeposits: number;
  totalWithdrawals: number;
  totalDividends: number;
  netCashFlow: number;
}

export interface CapitalFlowSummary {
  portfolioId: string;
  totalDeposits: number;
  totalWithdrawals: number;
  totalDividends: number;
  netCashFlow: number;
  flowCount: number;
}

export interface AdjustedReturn {
  portfolioId: string;
  timeWeightedReturn: number;
  moneyWeightedReturn: number;
  totalDeposits: number;
  totalWithdrawals: number;
  netCashFlow: number;
  currentValue: number;
  flowCount: number;
}

export interface RecordCapitalFlowRequest {
  portfolioId: string;
  type: string;
  amount: number;
  currency: string;
  note?: string;
  flowDate?: string;
}

@Injectable({
  providedIn: 'root'
})
export class CapitalFlowService {
  private readonly API_URL = `${environment.apiUrl}/capital-flows`;

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

  recordFlow(data: RecordCapitalFlowRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getFlowHistory(portfolioId: string, from?: string, to?: string): Observable<CapitalFlowHistory> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);

    return this.http.get<CapitalFlowHistory>(`${this.API_URL}/portfolio/${portfolioId}`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  getFlowSummary(portfolioId: string): Observable<CapitalFlowSummary> {
    return this.http.get<CapitalFlowSummary>(`${this.API_URL}/portfolio/${portfolioId}/summary`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTimeWeightedReturn(portfolioId: string): Observable<AdjustedReturn> {
    return this.http.get<AdjustedReturn>(`${this.API_URL}/portfolio/${portfolioId}/twr`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getMoneyWeightedReturn(portfolioId: string): Observable<AdjustedReturn> {
    return this.http.get<AdjustedReturn>(`${this.API_URL}/portfolio/${portfolioId}/mwr`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  deleteFlow(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('CapitalFlow API error:', error);
    return throwError(() => error);
  }
}

