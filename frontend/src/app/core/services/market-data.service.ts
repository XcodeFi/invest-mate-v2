import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

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
  close: number;
  priorClose: number;
  high: number;
  low: number;
  average: number;
  change: number;
  changePercent: number;
  volume: number;
  value: number;
  advance: number;
  decline: number;
  noChange: number;
  ceiling: number;
  floor: number;
  foreignBuyValue: number;
  foreignSellValue: number;
  foreignWeekBuyValue: number;
  foreignWeekSellValue: number;
  foreignMonthBuyValue: number;
  foreignMonthSellValue: number;
}

export interface MarketOverview {
  symbol: string;
  price: number;
  change: number;
  changePercent: number;
  totalVolume: number;
  totalValue: number;
  tradingStatus: number | null;
  foreignBuyValue: number;
  foreignSellValue: number;
}

export interface StockDetail {
  symbol: string;
  companyName: string;
  companyNameEng: string;
  shortName: string;
  exchange: string;
  floorCode: string;
  price: number;
  change: number;
  changePercent: number;
  referencePrice: number;
  openPrice: number;
  closePrice: number;
  highPrice: number;
  lowPrice: number;
  averagePrice: number;
  ceilingPrice: number;
  floorPrice: number;
  volume: number;
  value: number;
  foreignBuyVolume: number;
  foreignSellVolume: number;
  foreignRoom: number;
  logoUrl: string | null;
  bids: OrderBookLevel[];
  asks: OrderBookLevel[];
}

export interface OrderBookLevel {
  price: number;
  volume: number;
}

export interface StockSearchResult {
  symbol: string;
  companyName: string;
  shortName: string | null;
  exchange: string;
  logoUrl: string | null;
}

export interface TopFluctuation {
  symbol: string;
  companyName: string | null;
  shortName: string | null;
  price: number;
  change: number;
  changePercent: number;
  volume: number;
  ceilingPrice: number;
  floorPrice: number;
  referencePrice: number;
}

export interface TechnicalAnalysis {
  symbol: string;
  analyzedAt: string;
  dataPoints: number;
  currentPrice: number;
  priceChange: number;
  priceChangePercent: number;
  currentVolume: number;
  ema20?: number;
  ema50?: number;
  emaTrend: string;
  rsi14?: number;
  rsiSignal: string;
  macdLine?: number;
  signalLine?: number;
  macdHistogram?: number;
  macdSignal: string;
  avgVolume20?: number;
  volumeRatio?: number;
  volumeSignal: string;
  supportLevels: number[];
  resistanceLevels: number[];
  overallSignal: string;
  overallSignalVi: string;
  bullishCount: number;
  bearishCount: number;
  neutralCount: number;
  suggestedEntry?: number;
  suggestedStopLoss?: number;
  suggestedTarget?: number;
  riskRewardRatio?: number;
}

export interface TradingHistorySummary {
  symbol: string;
  changeDay: number;
  changeWeek: number;
  changeMonth: number;
  change3Month: number;
  change6Month: number;
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

  // --- Price endpoints ---

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

  // --- Index endpoints ---

  getMarketIndex(symbol: string): Observable<MarketIndex> {
    return this.http.get<MarketIndex>(`${this.API_URL}/index/${symbol}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getMarketOverview(): Observable<MarketOverview[]> {
    return this.http.get<MarketOverview[]>(`${this.API_URL}/overview`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  // --- Stock info endpoints ---

  getStockDetail(symbol: string): Observable<StockDetail> {
    return this.http.get<StockDetail>(`${this.API_URL}/stock/${symbol}/detail`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  searchStocks(keyword: string): Observable<StockSearchResult[]> {
    const params = new HttpParams().set('keyword', keyword);
    return this.http.get<StockSearchResult[]>(`${this.API_URL}/search`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  getTopFluctuation(floor: string = '10'): Observable<TopFluctuation[]> {
    const params = new HttpParams().set('floor', floor);
    return this.http.get<TopFluctuation[]>(`${this.API_URL}/top-fluctuation`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  getTechnicalAnalysis(symbol: string): Observable<TechnicalAnalysis> {
    return this.http.get<TechnicalAnalysis>(`${this.API_URL}/stock/${symbol}/analysis`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTradingSummary(symbol: string): Observable<TradingHistorySummary> {
    return this.http.get<TradingHistorySummary>(`${this.API_URL}/stock/${symbol}/summary`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('MarketData API error:', error);
    return throwError(() => error);
  }
}
