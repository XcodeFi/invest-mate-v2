import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface WatchlistSummary {
  id: string;
  name: string;
  emoji: string;
  isDefault: boolean;
  sortOrder: number;
  itemCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface WatchlistItem {
  symbol: string;
  note?: string;
  targetBuyPrice?: number;
  targetSellPrice?: number;
  addedAt: string;
}

export interface WatchlistDetail {
  id: string;
  name: string;
  emoji: string;
  isDefault: boolean;
  sortOrder: number;
  items: WatchlistItem[];
  createdAt: string;
  updatedAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class WatchlistService {
  private readonly API_URL = `${environment.apiUrl}/watchlists`;

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

  getAll(): Observable<WatchlistSummary[]> {
    return this.http.get<WatchlistSummary[]>(this.API_URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getDetail(id: string): Observable<WatchlistDetail> {
    return this.http.get<WatchlistDetail>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  create(data: { name: string; emoji?: string; isDefault?: boolean; sortOrder?: number }): Observable<WatchlistSummary> {
    return this.http.post<WatchlistSummary>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  update(id: string, data: { name: string; emoji?: string; sortOrder?: number }): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  addItem(watchlistId: string, data: {
    symbol: string;
    note?: string;
    targetBuyPrice?: number;
    targetSellPrice?: number;
  }): Observable<WatchlistDetail> {
    return this.http.post<WatchlistDetail>(
      `${this.API_URL}/${watchlistId}/items`, data, { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  updateItem(watchlistId: string, symbol: string, data: {
    note?: string;
    targetBuyPrice?: number;
    targetSellPrice?: number;
  }): Observable<WatchlistDetail> {
    return this.http.put<WatchlistDetail>(
      `${this.API_URL}/${watchlistId}/items/${symbol}`, data, { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  removeItem(watchlistId: string, symbol: string): Observable<WatchlistDetail> {
    return this.http.delete<WatchlistDetail>(
      `${this.API_URL}/${watchlistId}/items/${symbol}`, { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  importVn30(watchlistId?: string): Observable<WatchlistDetail> {
    return this.http.post<WatchlistDetail>(
      `${this.API_URL}/import-vn30`,
      { watchlistId },
      { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Watchlist API error:', error);
    return throwError(() => error);
  }
}
