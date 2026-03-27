import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface JournalEntry {
  id: string;
  userId: string;
  symbol: string;
  portfolioId?: string;
  tradeId?: string;
  tradePlanId?: string;
  entryType: string;
  title: string;
  content: string;
  marketContext?: string;
  emotionalState?: string;
  confidenceLevel?: number;
  priceAtTime?: number;
  vnIndexAtTime?: number;
  timestamp: string;
  tags: string[];
  rating?: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateJournalEntryRequest {
  symbol: string;
  entryType: string;
  title: string;
  content: string;
  portfolioId?: string;
  tradeId?: string;
  tradePlanId?: string;
  emotionalState?: string;
  confidenceLevel?: number;
  priceAtTime?: number;
  marketContext?: string;
  tags?: string[];
  timestamp?: string;
}

export interface UpdateJournalEntryRequest {
  title?: string;
  content?: string;
  entryType?: string;
  emotionalState?: string;
  confidenceLevel?: number;
  marketContext?: string;
  tags?: string[];
  rating?: number;
}

export interface SymbolTimeline {
  symbol: string;
  from?: string;
  to?: string;
  items: TimelineItem[];
  holdingPeriods: HoldingPeriod[];
  emotionSummary?: EmotionSummary;
}

export interface TimelineItem {
  type: 'journal' | 'trade' | 'alert' | 'event';
  timestamp: string;
  data: any;
}

export interface HoldingPeriod {
  startDate: string;
  endDate?: string;
  startQuantity: number;
  currentQuantity: number;
  changes: HoldingChange[];
}

export interface HoldingChange {
  date: string;
  type: string;
  quantity: number;
  price: number;
  remaining: number;
}

export interface EmotionSummary {
  distribution: { [key: string]: number };
  averageConfidence?: number;
  totalEntries: number;
}

export interface PendingReviewTrade {
  tradeId: string;
  symbol: string;
  portfolioId: string;
  portfolioName: string;
  price: number;
  quantity: number;
  tradeDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class JournalEntryService {
  private readonly API_URL = `${environment.apiUrl}/journal-entries`;
  private readonly TIMELINE_URL = `${environment.apiUrl}/symbols`;

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

  getPendingReview(portfolioId?: string): Observable<PendingReviewTrade[]> {
    let params = new HttpParams();
    if (portfolioId) params = params.set('portfolioId', portfolioId);
    return this.http.get<PendingReviewTrade[]>(`${this.API_URL}/pending-review`,
      { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  getBySymbol(symbol: string, from?: string, to?: string): Observable<JournalEntry[]> {
    let params = new HttpParams().set('symbol', symbol);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<JournalEntry[]>(this.API_URL, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  create(data: CreateJournalEntryRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  update(id: string, data: UpdateJournalEntryRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTimeline(symbol: string, from?: string, to?: string): Observable<SymbolTimeline> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<SymbolTimeline>(`${this.TIMELINE_URL}/${symbol}/timeline`,
      { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('JournalEntry API error:', error);
    return throwError(() => error);
  }
}
