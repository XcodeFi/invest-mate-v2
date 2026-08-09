import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export type CorporateActionType = 'CashDividend' | 'StockDividend' | 'StockSplit';

export interface CorporateAction {
  id: string;
  symbol: string;
  type: CorporateActionType;
  exDate: string;
  settlementDate?: string | null;
  settledAt?: string | null;
  amountPerShare?: number | null;
  multiplier: number;
  declaredText: string;
  note?: string | null;
}

export interface CreateCorporateActionPayload {
  PortfolioId: string;
  Symbol: string;
  Type: CorporateActionType;
  ExDate: string;
  SettlementDate?: string | null;
  PercentOfPar?: number | null;
  TaxRatePercent?: number | null;
  RatioOld?: number | null;
  RatioNew?: number | null;
  Note?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CorporateActionService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly apiUrl = `${environment.apiUrl}/corporate-actions`;

  // Không có interceptor auth toàn cục — mỗi service tự gắn token, giống các service khác.
  private getHeaders(): HttpHeaders {
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${this.authService.getToken()}`
    });
  }

  getByPortfolio(portfolioId: string, symbol?: string): Observable<CorporateAction[]> {
    let params = new HttpParams();
    if (symbol) params = params.set('symbol', symbol);

    return this.http
      .get<CorporateAction[]>(`${this.apiUrl}/portfolio/${portfolioId}`, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  // Body dùng PascalCase — API InvestmentApp.Api phân biệt hoa thường khi bind
  create(payload: CreateCorporateActionPayload): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(this.apiUrl, payload, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  settle(id: string, settledAt: string, linkExistingCapitalFlowId?: string): Observable<void> {
    return this.http
      .post<void>(`${this.apiUrl}/${id}/settle`, {
        SettledAt: settledAt,
        LinkExistingCapitalFlowId: linkExistingCapitalFlowId ?? null
      }, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Corporate action API error:', error);
    return throwError(() => error);
  }
}
