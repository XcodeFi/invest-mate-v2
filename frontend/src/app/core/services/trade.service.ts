import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface BulkTradeItem {
  symbol: string;
  tradeType: string;
  quantity: number;
  price: number;
  fee: number;
  tax: number;
  tradeDate?: string;
}

export interface BulkCreateResult {
  successCount: number;
  failedCount: number;
  errors: string[];
  createdIds: string[];
}

export interface CreateTradeRequest {
  portfolioId: string;
  symbol: string;
  tradeType: string;
  quantity: number;
  price: number;
  fee: number;
  tax: number;
  tradeDate?: string;
}

@Injectable({
  providedIn: 'root'
})
export class TradeService {
  private readonly API_URL = `${environment.apiUrl}/trades`;

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

  create(data: CreateTradeRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  linkToPlan(tradeId: string, planId: string): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${tradeId}/link-plan`, { planId }, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  bulkCreate(portfolioId: string, trades: BulkTradeItem[]): Observable<BulkCreateResult> {
    return this.http.post<BulkCreateResult>(`${this.API_URL}/bulk`, { portfolioId, trades }, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Trade API error:', error);
    return throwError(() => error);
  }
}
