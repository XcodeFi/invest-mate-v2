import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface PositionPnL {
  symbol: string;
  quantity: number;
  averageCost: number;
  currentPrice: number;
  marketValue: number;
  totalCost: number;
  unrealizedPnL: number;
  unrealizedPnLPercent: number;
  realizedPnL: number;
  totalPnL: number;
  totalPnLPercent: number;
}

export interface PortfolioPnL {
  portfolioId: string;
  portfolioName: string;
  initialCapital: number;
  totalInvested: number;
  totalMarketValue: number;
  totalRealizedPnL: number;
  totalUnrealizedPnL: number;
  totalPnL: number;
  totalPnLPercent: number;
  positions: PositionPnL[];
}

export interface OverallPnLSummary {
  totalPortfolios: number;
  totalInitialCapital: number;
  totalInvested: number;
  totalMarketValue: number;
  totalRealizedPnL: number;
  totalUnrealizedPnL: number;
  totalPnL: number;
  totalPnLPercent: number;
  portfolios: PortfolioPnL[];
}

@Injectable({
  providedIn: 'root'
})
export class PnlService {
  private readonly API_URL = `${environment.apiUrl}/pnl`;

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

  getPortfolioPnL(portfolioId: string): Observable<PortfolioPnL> {
    return this.http.get<PortfolioPnL>(`${this.API_URL}/portfolio/${portfolioId}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getPositionPnL(portfolioId: string, symbol: string): Observable<PositionPnL> {
    return this.http.get<PositionPnL>(`${this.API_URL}/portfolio/${portfolioId}/position/${symbol}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getSummary(): Observable<OverallPnLSummary> {
    return this.http.get<OverallPnLSummary>(`${this.API_URL}/summary`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('PnL API error:', error);
    return throwError(() => error);
  }
}
