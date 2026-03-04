import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';import { environment } from '../../../environments/environment';
export interface PortfolioSummary {
  id: string;
  name: string;
  initialCapital: number;
  createdAt: string;
  tradeCount: number;
  uniqueSymbols: number;
  totalInvested: number;
  totalSold: number;
}

export interface PortfolioDetail {
  id: string;
  name: string;
  initialCapital: number;
  createdAt: string;
  trades: TradeItem[];
}

export interface TradeItem {
  id: string;
  symbol: string;
  tradeType: string;
  quantity: number;
  price: number;
  fee: number;
  tax: number;
  tradeDate: string;
}

export interface CreatePortfolioRequest {
  name: string;
  initialCapital: number;
}

export interface UpdatePortfolioRequest {
  name: string;
  initialCapital: number;
}

@Injectable({
  providedIn: 'root'
})
export class PortfolioService {
  private readonly API_URL = `${environment.apiUrl}/portfolios`;

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

  getAll(): Observable<PortfolioSummary[]> {
    return this.http.get<PortfolioSummary[]>(this.API_URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getById(id: string): Observable<PortfolioDetail> {
    return this.http.get<PortfolioDetail>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  create(data: CreatePortfolioRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  update(id: string, data: UpdatePortfolioRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTrades(portfolioId: string, filters?: { symbol?: string; tradeType?: string; page?: number; pageSize?: number }): Observable<TradeListResponse> {
    let params = new HttpParams();
    if (filters?.symbol) params = params.set('symbol', filters.symbol);
    if (filters?.tradeType) params = params.set('tradeType', filters.tradeType);
    if (filters?.page) params = params.set('page', filters.page.toString());
    if (filters?.pageSize) params = params.set('pageSize', filters.pageSize.toString());

    return this.http.get<TradeListResponse>(`${this.API_URL}/${portfolioId}/trades`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Portfolio API error:', error);
    return throwError(() => error);
  }
}

export interface TradeListResponse {
  items: TradeResponseItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface TradeResponseItem {
  id: string;
  portfolioId: string;
  symbol: string;
  tradeType: string;
  quantity: number;
  price: number;
  fee: number;
  tax: number;
  tradeDate: string;
  totalValue: number;
}
