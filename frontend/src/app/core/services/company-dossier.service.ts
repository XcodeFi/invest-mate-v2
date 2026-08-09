import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export type DossierFreshness = 'Unconfirmed' | 'Fresh' | 'NeedsReview' | 'Expired';
export type DossierGateReason = 'missing' | 'unconfirmed' | 'expired' | 'insufficient';

// Việt hóa InvalidationTrigger — dùng cho dropdown SuggestedTrigger của risk factor.
export const INVALIDATION_TRIGGER_LABELS: Record<string, string> = {
  EarningsMiss: 'KQKD không đạt',
  TrendBreak: 'Gãy trend',
  NewsShock: 'Tin sốc',
  ThesisTimeout: 'Quá hạn chờ',
  Manual: 'Tự nhận định',
};

// Câu chữ cho từng lý do chặn — định nghĩa một chỗ duy nhất (supplement §3).
// 'insufficient' không có trong map này vì hiển thị theo missing[] của backend.
export const GATE_REASON_TEXT: Record<'missing' | 'unconfirmed' | 'expired', string> = {
  missing: 'Chưa có hồ sơ công ty cho mã này. Viết hồ sơ trước khi lập kế hoạch mua.',
  unconfirmed: 'Hồ sơ đã có nội dung nhưng chưa được ký xác nhận.',
  expired: 'Hồ sơ đã quá 180 ngày. Cập nhật tin mới rồi ký lại.',
};

export interface RiskFactorDto {
  rank: number;
  description: string;
  observableSignal: string;
  isDealBreaker: boolean;
  suggestedTrigger: string | null;
}

export interface CompanyDossierDto {
  symbol: string;
  businessModel: string;
  moats: { description: string }[];
  riskFactors: RiskFactorDto[];
  notes: string | null;
  reviewedAt: string;
  confirmedAt: string | null;
  agentDraftedAt: string | null;
  freshness: DossierFreshness;
}

export interface DossierUpsertPayload {
  BusinessModel: string;
  Moats: { Description: string }[];
  RiskFactors: {
    Rank: number;
    Description: string;
    ObservableSignal: string;
    IsDealBreaker: boolean;
    SuggestedTrigger: string | null;
  }[];
  Notes: string | null;
}

// freshness = null khi mã chưa có hồ sơ (supplement §2).
export interface DossierGateStatusDto {
  symbol: string;
  passed: boolean;
  reason: DossierGateReason | null;
  missing: string[];
  freshness: DossierFreshness | null;
}

@Injectable({ providedIn: 'root' })
export class CompanyDossierService {
  private readonly base = `${environment.apiUrl}/company-dossiers`;

  constructor(private http: HttpClient, private authService: AuthService) {}

  // Không có auth interceptor toàn cục — mỗi lời gọi phải tự gắn header (supplement §6).
  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    });
  }

  list(): Observable<CompanyDossierDto[]> {
    return this.http.get<CompanyDossierDto[]>(this.base, { headers: this.getHeaders() });
  }

  get(symbol: string): Observable<CompanyDossierDto> {
    return this.http.get<CompanyDossierDto>(`${this.base}/${symbol}`, { headers: this.getHeaders() });
  }

  upsert(symbol: string, payload: DossierUpsertPayload): Observable<{ id: string }> {
    return this.http.put<{ id: string }>(`${this.base}/${symbol}`, payload, { headers: this.getHeaders() });
  }

  confirm(symbol: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${symbol}/confirm`, {}, { headers: this.getHeaders() });
  }

  // Ba tham số bắt buộc, không optional — thiếu cái nào backend trả 400 (supplement §1).
  // Đừng gọi hàm này khi form chưa có đủ cả ba số; đừng thay giá trị thiếu bằng 0.
  gateStatus(symbol: string, quantity: number, entryPrice: number, accountBalance: number): Observable<DossierGateStatusDto> {
    const params = new HttpParams()
      .set('quantity', String(quantity))
      .set('entryPrice', String(entryPrice))
      .set('accountBalance', String(accountBalance));
    return this.http.get<DossierGateStatusDto>(`${this.base}/${symbol}/gate-status`, {
      headers: this.getHeaders(),
      params,
    });
  }
}
