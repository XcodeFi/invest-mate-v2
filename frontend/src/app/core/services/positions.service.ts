import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { TradePlan } from './trade-plan.service';

export interface TradeSummary {
  id: string;
  tradeType: string;
  quantity: number;
  price: number;
  tradeDate: string;
}

export interface ActivePosition {
  symbol: string;
  portfolioId: string;
  portfolioName: string;
  quantity: number;
  averageCost: number;
  currentPrice: number;
  marketValue: number;
  unrealizedPnL: number;
  unrealizedPnLPercent: number;
  realizedPnL: number;
  linkedPlan?: TradePlan;
  recentTrades: TradeSummary[];
  nextAction?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PositionsService {
  private readonly API_URL = `${environment.apiUrl}/positions`;

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

  getAll(portfolioId?: string): Observable<ActivePosition[]> {
    let params = new HttpParams();
    if (portfolioId) {
      params = params.set('portfolioId', portfolioId);
    }
    return this.http.get<ActivePosition[]>(this.API_URL, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Positions API error:', error);
    return throwError(() => error);
  }
}
