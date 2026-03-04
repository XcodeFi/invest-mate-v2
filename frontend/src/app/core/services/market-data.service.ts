import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';import { environment } from '../../../environments/environment';
export interface StockPrice {
  symbol: string;
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface BatchPrice {
  symbol: string;
  date: string;
  close: number;
  volume: number;
}

export interface MarketIndex {
  indexSymbol: string;
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  change: number;
  changePercent: number;
}

@Injectable({
  providedIn: 'root'
})
export class MarketDataService {
  private readonly API_URL = `${environment.apiUrl}/market`;

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

  getCurrentPrice(symbol: string): Observable<StockPrice> {
    return this.http.get<StockPrice>(`${this.API_URL}/price/${symbol}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getPriceHistory(symbol: string, from?: string, to?: string): Observable<StockPrice[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);

    return this.http.get<StockPrice[]>(`${this.API_URL}/price/${symbol}/history`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  getBatchPrices(symbols: string[]): Observable<BatchPrice[]> {
    const params = new HttpParams().set('symbols', symbols.join(','));
    return this.http.get<BatchPrice[]>(`${this.API_URL}/prices`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  getMarketIndex(symbol: string): Observable<MarketIndex> {
    return this.http.get<MarketIndex>(`${this.API_URL}/index/${symbol}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('MarketData API error:', error);
    return throwError(() => error);
  }
}
