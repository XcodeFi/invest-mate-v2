import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface ChecklistItemDto {
  label: string;
  category: string;
  checked: boolean;
  critical: boolean;
  hint: string;
}

export interface PlanLotDto {
  lotNumber: number;
  plannedPrice: number;
  plannedQuantity: number;
  allocationPercent?: number;
  label?: string;
  status: string;
  actualPrice?: number;
  executedAt?: string;
  tradeId?: string;
}

export interface ExitTargetDto {
  level: number;
  actionType: string;
  price: number;
  quantity?: number;
  percentOfPosition?: number;
  label?: string;
  isTriggered: boolean;
  triggeredAt?: string;
  tradeId?: string;
}

export interface StopLossHistoryDto {
  oldPrice: number;
  newPrice: number;
  reason?: string;
  changedAt: string;
}

export interface TrailingStopConfigDto {
  method: string;
  trailValue: number;
  activationPrice?: number;
  stepSize?: number;
  currentTrailingStop?: number;
  highestPrice?: number;
}

export interface ScenarioNodeDto {
  nodeId: string;
  parentId: string | null;
  order: number;
  label: string;
  conditionType: string;
  conditionValue: number | null;
  conditionNote: string | null;
  actionType: string;
  actionValue: number | null;
  trailingStopConfig: TrailingStopConfigDto | null;
  status: string;
  triggeredAt: string | null;
  tradeId: string | null;
}

export interface ScenarioPreset {
  id: string;
  name: string;
  nameVi: string;
  description: string;
  nodes: ScenarioNodeDto[];
  isPreset: boolean;
}

export interface ScenarioHistoryDto {
  nodeId: string;
  label: string;
  status: string;
  triggeredAt: string | null;
  priceAtTrigger: number | null;
  actionType: string;
  actionValue: number | null;
  conditionType: string;
  conditionValue: number | null;
  parentId: string | null;
}

export interface TradePlan {
  id: string;
  portfolioId?: string;
  symbol: string;
  direction: string;
  entryPrice: number;
  stopLoss: number;
  target: number;
  quantity: number;
  strategyId?: string;
  marketCondition: string;
  reason?: string;
  notes?: string;
  riskPercent?: number;
  accountBalance?: number;
  riskRewardRatio?: number;
  confidenceLevel: number;
  checklist: ChecklistItemDto[];
  entryMode?: string;
  lots?: PlanLotDto[];
  exitTargets?: ExitTargetDto[];
  stopLossHistory?: StopLossHistoryDto[];
  exitStrategyMode?: string;
  scenarioNodes?: ScenarioNodeDto[];
  timeHorizon?: string;
  reviewData?: CampaignReviewDataDto;
  status: string;
  tradeId?: string;
  tradeIds?: string[];
  executedAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTradePlanRequest {
  portfolioId?: string;
  symbol: string;
  direction: string;
  entryPrice: number;
  stopLoss: number;
  target: number;
  quantity: number;
  strategyId?: string;
  marketCondition?: string;
  reason?: string;
  notes?: string;
  riskPercent?: number;
  accountBalance?: number;
  riskRewardRatio?: number;
  confidenceLevel?: number;
  checklist?: ChecklistItemDto[];
  entryMode?: string;
  lots?: PlanLotDto[];
  exitTargets?: ExitTargetDto[];
  exitStrategyMode?: string;
  scenarioNodes?: ScenarioNodeDto[];
  timeHorizon?: string;
  status?: string;
  tradeId?: string;
}

export interface UpdateTradePlanRequest {
  portfolioId?: string;
  symbol?: string;
  direction?: string;
  entryPrice?: number;
  stopLoss?: number;
  target?: number;
  quantity?: number;
  strategyId?: string;
  marketCondition?: string;
  reason?: string;
  notes?: string;
  riskPercent?: number;
  accountBalance?: number;
  riskRewardRatio?: number;
  confidenceLevel?: number;
  checklist?: ChecklistItemDto[];
  entryMode?: string;
  lots?: PlanLotDto[];
  exitTargets?: ExitTargetDto[];
  exitStrategyMode?: string;
  scenarioNodes?: ScenarioNodeDto[];
  timeHorizon?: string;
}

export interface UpdateTradePlanStatusRequest {
  status: string;
  tradeId?: string;
}

export interface SuggestedNodeDto {
  nodeId: string;
  parentId?: string;
  order: number;
  label: string;
  conditionType: string;
  conditionValue?: number;
  actionType: string;
  actionValue?: number;
  reasoning: string;
  category: string;
}

export interface ScenarioSuggestionDto {
  symbol: string;
  entryPrice: number;
  timeHorizon: string;
  technicalBasis: {
    ema20?: number;
    ema50?: number;
    ema200?: number;
    rsi14?: number;
    bollingerUpper?: number;
    bollingerLower?: number;
    supportLevels: number[];
    resistanceLevels: number[];
    fibonacci?: any;
    atr14?: number;
  };
  nodes: SuggestedNodeDto[];
}

export interface CampaignReviewDataDto {
  pnLAmount: number;
  pnLPercent: number;
  holdingDays: number;
  pnLPerDay: number;
  annualizedReturnPercent: number;
  targetAchievementPercent: number;
  totalInvested: number;
  totalReturned: number;
  totalFees: number;
  lessonsLearned?: string;
  reviewedAt: string;
}

export interface CampaignReviewDto {
  pnLAmount: number;
  pnLPercent: number;
  holdingDays: number;
  pnLPerDay: number;
  annualizedReturnPercent: number;
  targetAchievementPercent: number;
  totalInvested: number;
  totalReturned: number;
  totalFees: number;
  lessonsLearned?: string;
  reviewedAt: string;
}

export interface CampaignAnalyticsDto {
  totalCampaigns: number;
  winningCampaigns: number;
  losingCampaigns: number;
  winRate: number;
  averagePnLPercent: number;
  averagePnLPerDay: number;
  totalAccumulatedPnL: number;
  averageHoldingDays: number;
  bestCampaign?: CampaignSummaryDto;
  worstCampaign?: CampaignSummaryDto;
  trend: CampaignTrendDto[];
}

export interface CampaignSummaryDto {
  planId: string;
  symbol: string;
  pnLAmount: number;
  pnLPercent: number;
  pnLPerDay: number;
  holdingDays: number;
  timeHorizon?: string;
  reviewedAt: string;
}

export interface CampaignTrendDto {
  planId: string;
  symbol: string;
  reviewedAt: string;
  pnLPercent: number;
  cumulativePnL: number;
}

export interface ScenarioAdvisoryDto {
  tradePlanId: string;
  symbol: string;
  currentPrice: number;
  nodeId: string;
  nodeLabel: string;
  conditionDescription: string;
  actionDescription: string;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class TradePlanService {
  private readonly API_URL = `${environment.apiUrl}/trade-plans`;

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

  getAll(activeOnly?: boolean): Observable<TradePlan[]> {
    let params = new HttpParams();
    if (activeOnly) {
      params = params.set('activeOnly', 'true');
    }
    return this.http.get<TradePlan[]>(this.API_URL, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  getById(id: string): Observable<TradePlan> {
    return this.http.get<TradePlan>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  create(data: CreateTradePlanRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.API_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  update(id: string, data: UpdateTradePlanRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  updateStatus(id: string, data: UpdateTradePlanStatusRequest): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${id}/status`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  executeLot(planId: string, lotNumber: number, data: { tradeId: string; actualPrice: number }): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${planId}/lots/${lotNumber}/execute`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  updateStopLoss(planId: string, data: { newStopLoss: number; reason?: string }): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${planId}/stop-loss`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  triggerExitTarget(planId: string, level: number, data: { tradeId: string }): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${planId}/exit-targets/${level}/trigger`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  triggerScenarioNode(planId: string, nodeId: string, data: { tradeId?: string }): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${planId}/scenario-nodes/${nodeId}/trigger`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getScenarioTemplates(): Observable<ScenarioPreset[]> {
    return this.http.get<ScenarioPreset[]>(`${this.API_URL}/scenario-templates`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getScenarioHistory(planId: string): Observable<ScenarioHistoryDto[]> {
    return this.http.get<ScenarioHistoryDto[]>(`${this.API_URL}/${planId}/scenario-history`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  saveScenarioTemplate(data: { name: string; description: string; nodes: ScenarioNodeDto[] }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.API_URL}/scenario-templates`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  deleteScenarioTemplate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/scenario-templates/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getScenarioSuggestion(symbol: string, entryPrice: number, timeHorizon: string): Observable<ScenarioSuggestionDto> {
    let params = new HttpParams()
      .set('symbol', symbol)
      .set('entryPrice', entryPrice.toString())
      .set('timeHorizon', timeHorizon);
    return this.http.get<ScenarioSuggestionDto>(`${this.API_URL}/scenario-suggestion`, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  getAdvisories(): Observable<ScenarioAdvisoryDto[]> {
    return this.http.get<ScenarioAdvisoryDto[]>(`${this.API_URL}/advisories`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  // --- Campaign Review ---

  previewReview(planId: string): Observable<CampaignReviewDto> {
    return this.http.get<CampaignReviewDto>(`${this.API_URL}/${planId}/review/preview`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  submitReview(planId: string, data: { lessonsLearned?: string }): Observable<CampaignReviewDto> {
    return this.http.post<CampaignReviewDto>(`${this.API_URL}/${planId}/review`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  updateReviewLessons(planId: string, data: { lessonsLearned: string }): Observable<void> {
    return this.http.patch<void>(`${this.API_URL}/${planId}/review/lessons`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getPendingReview(): Observable<TradePlan[]> {
    return this.http.get<TradePlan[]>(`${this.API_URL}/pending-review`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getCampaignAnalytics(timeHorizon?: string): Observable<CampaignAnalyticsDto> {
    let params = new HttpParams();
    if (timeHorizon) params = params.set('timeHorizon', timeHorizon);
    return this.http.get<CampaignAnalyticsDto>(`${this.API_URL}/campaign-analytics`, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('TradePlan API error:', error);
    return throwError(() => error);
  }
}
