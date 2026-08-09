import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

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
  private readonly apiUrl = `${environment.apiUrl}/corporate-actions`;

  getByPortfolio(portfolioId: string, symbol?: string): Observable<CorporateAction[]> {
    const query = symbol ? `?symbol=${encodeURIComponent(symbol)}` : '';
    return this.http.get<CorporateAction[]>(`${this.apiUrl}/portfolio/${portfolioId}${query}`);
  }

  // Body dùng PascalCase — API InvestmentApp.Api phân biệt hoa thường khi bind
  create(payload: CreateCorporateActionPayload): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.apiUrl, payload);
  }

  settle(id: string, settledAt: string, linkExistingCapitalFlowId?: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/settle`, {
      SettledAt: settledAt,
      LinkExistingCapitalFlowId: linkExistingCapitalFlowId ?? null
    });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
