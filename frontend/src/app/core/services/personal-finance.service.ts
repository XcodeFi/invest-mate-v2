import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

// ⚠️ WARNING: Enum values MUST match backend Domain enum numeric serialization exactly.
// Backend uses default System.Text.Json int serialization (no JsonStringEnumConverter).
// Nếu BE thêm JsonStringEnumConverter, FE sẽ nhận string ("SJC" thay vì 0) → broken silently.
// GoldBrand.Other = 99 intentional (gap để phân biệt khỏi "brand chưa biết" trong tương lai).
export enum FinancialAccountType {
  Securities = 0,
  Savings = 1,
  Emergency = 2,
  IdleCash = 3,
  Gold = 4,
}

export enum GoldBrand {
  SJC = 0,
  DOJI = 1,
  PNJ = 2,
  Other = 99,
}

export enum GoldType {
  Mieng = 0,
  Nhan = 1,
}

export interface FinancialRulesDto {
  emergencyFundMonths: number;
  maxInvestmentPercent: number;
  minSavingsPercent: number;
}

export interface FinancialAccountDto {
  id: string;
  type: FinancialAccountType;
  name: string;
  balance: number;
  interestRate?: number | null;
  note?: string | null;
  goldBrand?: GoldBrand | null;
  goldType?: GoldType | null;
  goldQuantity?: number | null;
  updatedAt: string;
}

export interface FinancialProfileDto {
  id: string;
  userId: string;
  monthlyExpense: number;
  accounts: FinancialAccountDto[];
  rules: FinancialRulesDto;
  createdAt: string;
  updatedAt: string;
}

export interface RuleCheckResultDto {
  ruleName: string;
  isPassing: boolean;
  description: string;
  currentValue: number;
  requiredValue: number;
}

export interface NetWorthSummaryDto {
  hasProfile: boolean;
  totalAssets: number;
  securitiesValue: number;
  goldTotal: number;
  savingsTotal: number;
  emergencyTotal: number;
  idleCashTotal: number;
  monthlyExpense: number;
  healthScore: number;
  ruleChecks: RuleCheckResultDto[];
  accounts: FinancialAccountDto[];
}

export interface GoldPriceDto {
  brand: GoldBrand;
  type: GoldType;
  buyPrice: number;
  sellPrice: number;
  updatedAt: string;
}

export interface UpsertFinancialProfileRequest {
  monthlyExpense?: number;
  emergencyFundMonths?: number;
  maxInvestmentPercent?: number;
  minSavingsPercent?: number;
}

export interface UpsertFinancialAccountRequest {
  accountId?: string | null;
  type: FinancialAccountType;
  name: string;
  balance?: number | null;
  interestRate?: number | null;
  note?: string | null;
  goldBrand?: GoldBrand | null;
  goldType?: GoldType | null;
  goldQuantity?: number | null;
}

@Injectable({ providedIn: 'root' })
export class PersonalFinanceService {
  private BASE = `${environment.apiUrl}/personal-finance`;
  private http = inject(HttpClient);
  private auth = inject(AuthService);

  private headers(): HttpHeaders {
    const token = this.auth.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    });
  }

  /** Trả null nếu profile chưa tồn tại (backend 404 → service convert null). */
  getProfile(): Observable<FinancialProfileDto | null> {
    return this.http.get<FinancialProfileDto>(this.BASE, { headers: this.headers() }).pipe(
      catchError(err => err?.status === 404 ? of(null) : throwError(() => err)),
    );
  }

  getSummary(): Observable<NetWorthSummaryDto> {
    return this.http.get<NetWorthSummaryDto>(`${this.BASE}/summary`, { headers: this.headers() });
  }

  getGoldPrices(): Observable<GoldPriceDto[]> {
    return this.http.get<GoldPriceDto[]>(`${this.BASE}/gold-prices`, { headers: this.headers() });
  }

  upsertProfile(data: UpsertFinancialProfileRequest): Observable<FinancialProfileDto> {
    return this.http.put<FinancialProfileDto>(this.BASE, data, { headers: this.headers() });
  }

  upsertAccount(data: UpsertFinancialAccountRequest): Observable<FinancialAccountDto> {
    return this.http.put<FinancialAccountDto>(`${this.BASE}/accounts`, data, { headers: this.headers() });
  }

  removeAccount(accountId: string): Observable<void> {
    return this.http.delete<void>(`${this.BASE}/accounts/${accountId}`, { headers: this.headers() });
  }

  // ── Label helpers ─────────────────────────────────────────────────────────

  static accountTypeLabel(type: FinancialAccountType): string {
    switch (type) {
      case FinancialAccountType.Securities: return 'Chứng khoán';
      case FinancialAccountType.Savings: return 'Tiết kiệm';
      case FinancialAccountType.Emergency: return 'Quỹ dự phòng';
      case FinancialAccountType.IdleCash: return 'Tiền nhàn rỗi';
      case FinancialAccountType.Gold: return 'Vàng';
      default: return 'Khác';
    }
  }

  static accountTypeIcon(type: FinancialAccountType): string {
    switch (type) {
      case FinancialAccountType.Securities: return '📈';
      case FinancialAccountType.Savings: return '🏦';
      case FinancialAccountType.Emergency: return '🛡️';
      case FinancialAccountType.IdleCash: return '💵';
      case FinancialAccountType.Gold: return '🪙';
      default: return '💰';
    }
  }

  static goldBrandLabel(brand: GoldBrand): string {
    switch (brand) {
      case GoldBrand.SJC: return 'SJC';
      case GoldBrand.DOJI: return 'DOJI';
      case GoldBrand.PNJ: return 'PNJ';
      case GoldBrand.Other: return 'Khác (BTMC/BTMH/...)';
      default: return String(brand);
    }
  }

  static goldTypeLabel(type: GoldType): string {
    switch (type) {
      case GoldType.Mieng: return 'Vàng miếng';
      case GoldType.Nhan: return 'Vàng nhẫn';
      default: return String(type);
    }
  }
}
