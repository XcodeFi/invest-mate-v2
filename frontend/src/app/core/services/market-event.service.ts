import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface MarketEvent {
  id: string;
  symbol: string;
  eventType: string;
  title: string;
  description?: string;
  source?: string;
  eventDate: string;
  createdAt: string;
}

export interface CreateMarketEventRequest {
  symbol: string;
  eventType: string;
  title: string;
  eventDate: string;
  description?: string;
  source?: string;
}

@Injectable({
  providedIn: 'root'
})
export class MarketEventService {
  private readonly API_URL = `${environment.apiUrl}/market-events`;

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

  getBySymbol(symbol: string, from?: string, to?: string): Observable<MarketEvent[]> {
    let params = new HttpParams().set('symbol', symbol);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<MarketEvent[]>(this.API_URL, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  create(data: CreateMarketEventRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('MarketEvent API error:', error);
    return throwError(() => error);
  }
}
