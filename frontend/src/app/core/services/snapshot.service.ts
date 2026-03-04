import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface PositionSnapshot {
  symbol: string;
  quantity: number;
  averageCost: number;
  marketPrice: number;
  marketValue: number;
  unrealizedPnL: number;
  weight: number;
}

export interface Snapshot {
  id: string;
  portfolioId: string;
  snapshotDate: string;
  totalValue: number;
  cashBalance: number;
  investedValue: number;
  unrealizedPnL: number;
  realizedPnL: number;
  dailyReturn: number;
  cumulativeReturn: number;
  positions: PositionSnapshot[];
}

export interface SnapshotComparison {
  portfolioId: string;
  snapshot1: Snapshot | null;
  snapshot2: Snapshot | null;
  valueChange: number;
  valueChangePercent: number;
  returnDifference: number;
}

@Injectable({
  providedIn: 'root'
})
export class SnapshotService {
  private readonly API_URL = `${environment.apiUrl}/snapshots`;

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

  getSnapshotAtDate(portfolioId: string, date: string): Observable<Snapshot> {
    return this.http.get<Snapshot>(`${this.API_URL}/portfolio/${portfolioId}/at/${date}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getSnapshotRange(portfolioId: string, from?: string, to?: string): Observable<Snapshot[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);

    return this.http.get<Snapshot[]>(`${this.API_URL}/portfolio/${portfolioId}/range`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  compareSnapshots(portfolioId: string, date1: string, date2: string): Observable<SnapshotComparison> {
    const params = new HttpParams()
      .set('date1', date1)
      .set('date2', date2);

    return this.http.get<SnapshotComparison>(`${this.API_URL}/portfolio/${portfolioId}/compare`, {
      headers: this.getHeaders(),
      params
    }).pipe(catchError(this.handleError));
  }

  takeSnapshot(portfolioId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.API_URL}/portfolio/${portfolioId}/take`, {}, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getTimeline(portfolioId: string): Observable<Snapshot[]> {
    return this.http.get<Snapshot[]>(`${this.API_URL}/portfolio/${portfolioId}/timeline`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Snapshot API error:', error);
    return throwError(() => error);
  }
}

