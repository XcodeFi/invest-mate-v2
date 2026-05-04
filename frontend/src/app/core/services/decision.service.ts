import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/**
 * Decision Queue — gộp 3 nguồn alert (StopLoss / Scenario trigger / Thesis review)
 * thành 1 list duy nhất ép user xử lý quyết định kỷ luật.
 * Xem `docs/plans/dashboard-decision-engine.md` §5 (P3).
 */
export type DecisionType = 'StopLossHit' | 'ScenarioTrigger' | 'ThesisReviewDue';
export type DecisionSeverity = 'Critical' | 'Warning' | 'Info';

export interface DecisionItemDto {
  id: string;
  type: DecisionType;
  severity: DecisionSeverity;
  symbol: string;
  portfolioId: string;
  portfolioName: string;
  headline: string;
  thesisOrReason: string | null;
  currentPrice: number | null;
  plannedExitPrice: number | null;
  tradePlanId: string | null;
  dueAt: string | null;
  createdAt: string;
}

export interface DecisionQueueDto {
  items: DecisionItemDto[];
  totalCount: number;
}

/**
 * Action mà user chọn cho 1 DecisionItem inline.
 * `ExecuteSell` = BÁN theo plan. `HoldWithJournal` = GIỮ + ghi lý do.
 * Khớp với backend enum `DecisionAction` (P4 — xem `docs/plans/dashboard-decision-engine.md` §6).
 */
export type DecisionAction = 'ExecuteSell' | 'HoldWithJournal';

export interface ResolveDecisionRequest {
  action: DecisionAction;
  tradePlanId?: string | null;
  symbol?: string | null;
  note?: string | null;
}

export interface ResolveDecisionResult {
  resultId: string;
  message: string;
  resultType: 'Trade' | 'JournalEntry';
}

@Injectable({ providedIn: 'root' })
export class DecisionService {
  private readonly API_URL = `${environment.apiUrl}/decisions`;

  constructor(private http: HttpClient, private authService: AuthService) {}

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    });
  }

  getQueue(): Observable<DecisionQueueDto> {
    return this.http
      .get<DecisionQueueDto>(`${this.API_URL}/queue`, { headers: this.getHeaders() })
      .pipe(catchError((err) => throwError(() => err)));
  }

  resolve(decisionId: string, request: ResolveDecisionRequest): Observable<ResolveDecisionResult> {
    // PascalCase JSON keys (case-sensitive backend binding — see memory `learning_toolquirk_api_pascalcase_required.md`).
    const body = {
      Action: request.action,
      TradePlanId: request.tradePlanId ?? null,
      Symbol: request.symbol ?? null,
      Note: request.note ?? null,
    };
    return this.http
      .post<ResolveDecisionResult>(
        `${this.API_URL}/${encodeURIComponent(decisionId)}/resolve`,
        body,
        { headers: this.getHeaders() }
      )
      .pipe(catchError((err) => throwError(() => err)));
  }
}
