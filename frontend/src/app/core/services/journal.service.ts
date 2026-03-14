import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface TradeJournal {
  id: string;
  tradeId: string;
  portfolioId: string;
  entryReason: string;
  marketContext: string;
  technicalSetup: string;
  emotionalState: string;
  confidenceLevel: number;
  postTradeReview: string;
  lessonsLearned: string;
  rating: number;
  tags: string[];
  tradePlanId?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateJournalRequest {
  tradeId: string;
  portfolioId: string;
  entryReason?: string;
  marketContext?: string;
  technicalSetup?: string;
  emotionalState?: string;
  confidenceLevel?: number;
  postTradeReview?: string;
  lessonsLearned?: string;
  rating?: number;
  tags?: string[];
}

export interface UpdateJournalRequest {
  entryReason?: string;
  marketContext?: string;
  technicalSetup?: string;
  emotionalState?: string;
  confidenceLevel?: number;
  postTradeReview?: string;
  lessonsLearned?: string;
  rating?: number;
  tags?: string[];
}

@Injectable({
  providedIn: 'root'
})
export class JournalService {
  private readonly API_URL = `${environment.apiUrl}/journals`;

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

  getAll(portfolioId?: string): Observable<TradeJournal[]> {
    let params = new HttpParams();
    if (portfolioId) {
      params = params.set('portfolioId', portfolioId);
    }
    return this.http.get<TradeJournal[]>(this.API_URL, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  getByTrade(tradeId: string): Observable<TradeJournal> {
    return this.http.get<TradeJournal>(`${this.API_URL}/trade/${tradeId}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  create(data: CreateJournalRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  update(id: string, data: UpdateJournalRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Journal API error:', error);
    return throwError(() => error);
  }
}

