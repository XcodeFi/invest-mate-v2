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

export type InvalidationTrigger =
  | 'EarningsMiss'
  | 'TrendBreak'
  | 'NewsShock'
  | 'ThesisTimeout'
  | 'Manual';

export interface InvalidationRuleDto {
  trigger: InvalidationTrigger;
  detail: string;
  checkDate?: string | null;
  isTriggered: boolean;
  triggeredAt?: string | null;
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
  thesis?: string;
  invalidationCriteria?: InvalidationRuleDto[];
  expectedReviewDate?: string | null;
  legacyExempt?: boolean;
  notes?: string;
  riskPercent?: number;
  accountBalance?: number;
  riskRewardRatio?: number;
  /** R:R hiệu lực khi đọc: số hệ thống tính khi đủ dữ liệu, không thì giá trị đã gõ. */
  effectiveRiskReward?: number | null;
  riskRewardComputed?: boolean;
  /** Bảng kịch bản kết quả hiện hành (null với kế hoạch cũ hoặc lệnh bán). */
  scenarios?: OutcomeScenarioDto[] | null;
  /** Ảnh chụp ước lượng lúc chốt kế hoạch; khoá ở lần khớp đầu. */
  estimate?: EstimateDto | null;
  /** Kỳ vọng tính ở server (đã trừ phí ước tính). Trường null = chưa tính được. */
  expectation?: PlanExpectationDto | null;
  evGateReason?: string | null;
  /** Vào lệnh không qua cổng kỳ vọng. */
  evGateSkipped?: boolean;
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

export interface OutcomeScenarioDto {
  label: string;
  probabilityPercent: number;
  exitPrice: number;
}

export interface EstimateDto {
  scenarios: OutcomeScenarioDto[];
  evR: number | null;
  impliedWinProbability: number | null;
  feesIncluded: boolean;
  capturedAt: string;
  locked: boolean;
  lockedAt?: string | null;
}

export interface PlanExpectationDto {
  evR: number | null;
  impliedWinPercent: number | null;
  /** EV theo VND = EV theo R × 1R. */
  evAmount: number | null;
}

export interface CreateTradePlanRequest {
  scenarios?: OutcomeScenarioDto[];
  /** Lý do vượt cổng kỳ vọng khi tạo thẳng ở Ready (EV ≤ 0 hoặc chưa ước lượng). */
  evGateReason?: string;
  portfolioId?: string;
  symbol: string;
  direction: string;
  entryPrice: number;
  stopLoss: number;
  target: number;
  quantity: number;
  strategyId?: string;
  marketCondition?: string;
  thesis?: string;
  invalidationCriteria?: InvalidationRuleDto[];
  expectedReviewDate?: string | null;
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
  /** Bỏ trống = giữ nguyên; mảng rỗng = xoá bảng. */
  scenarios?: OutcomeScenarioDto[];
  portfolioId?: string;
  symbol?: string;
  direction?: string;
  entryPrice?: number;
  stopLoss?: number;
  target?: number;
  quantity?: number;
  strategyId?: string;
  marketCondition?: string;
  thesis?: string;
  invalidationCriteria?: InvalidationRuleDto[];
  expectedReviewDate?: string | null;
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
  /** Bắt buộc khi chuyển ready mà EV ≤ 0 hoặc chưa ước lượng (≥ 10 ký tự). */
  evGateReason?: string;
}

export interface AbortTradePlanResult {
  planId: string;
  status: string;
  trigger: InvalidationTrigger;
  tradeIdsAffected: string[];
  abortedAt: string;
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
  /** PnL ròng / 1R ban đầu; null nếu không tính được (hiện n/a). */
  realizedR?: number | null;
  /** chot-ke-hoach | uoc-tinh | khop-mot-phan */
  riskBasis?: string | null;
  /** Số CP còn giữ lúc review; lớn hơn 0 nghĩa là P&L/R chỉ tính phần đã bán. null = review cũ. */
  openQuantity?: number | null;
  /** Cổ tức tiền mặt ròng theo quyền hưởng, đã nằm trong P&L. */
  dividendIncome?: number | null;
  /** Giá vốn của phần đã bán (mẫu số của % lãi/lỗ). */
  realizedCost?: number | null;
  /** Số CP bán nhưng chưa có lệnh mua đã gắn; lớn hơn 0 thì P&L chỉ phần khớp và R = null. */
  oversoldQuantity?: number | null;
  /** thieu-lenh-mua-lien-ket | lenh-ban-o-ke-hoach-khac */
  flags?: string[] | null;
  /** Số liệu trước lần tính lại đầu tiên. */
  legacy?: ReviewLegacyDto | null;
}

export interface ReviewLegacyDto {
  pnLAmount: number;
  pnLPercent: number;
  realizedR?: number | null;
  recomputedAt: string;
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
  /** PnL ròng / 1R ban đầu; null nếu không tính được (hiện n/a). */
  realizedR?: number | null;
  /** chot-ke-hoach | uoc-tinh | khop-mot-phan */
  riskBasis?: string | null;
  /** Số CP còn giữ lúc review; lớn hơn 0 nghĩa là P&L/R chỉ tính phần đã bán. null = review cũ. */
  openQuantity?: number | null;
  /** Cổ tức tiền mặt ròng theo quyền hưởng, đã nằm trong P&L. */
  dividendIncome?: number | null;
  /** Giá vốn của phần đã bán (mẫu số của % lãi/lỗ). */
  realizedCost?: number | null;
  /** Số CP bán nhưng chưa có lệnh mua đã gắn; lớn hơn 0 thì P&L chỉ phần khớp và R = null. */
  oversoldQuantity?: number | null;
  /** thieu-lenh-mua-lien-ket | lenh-ban-o-ke-hoach-khac */
  flags?: string[] | null;
  /** Số liệu trước lần tính lại đầu tiên. */
  legacy?: ReviewLegacyDto | null;
}

/** Kỳ vọng theo R của các chiến dịch đã đóng (kế hoạch Mua đã review). Giá trị null = không đủ dữ liệu (hiện n/a). */
export interface ExpectancyStatsDto {
  strategyId: string | null;
  strategyName: string;
  n: number;
  wins: number;
  losses: number;
  draws: number;
  winRate: number | null;
  winRateLow95: number | null;
  winRateHigh95: number | null;
  avgWinR: number | null;
  avgLossR: number | null;
  expectancyR: number | null;
  profitFactorR: number | null;
}

export interface StrategyExpectancyDto {
  total: ExpectancyStatsDto;
  strategies: ExpectancyStatsDto[];
}

/** Một mức ước lượng của độ khớp. Mọi tỉ lệ là phân số 0–1; null = chưa có mẫu. */
export interface CalibrationBucketDto {
  label: string;
  low: number;
  high: number;
  n: number;
  avgEstimated: number | null;
  actualWinRate: number | null;
  /** false khi n < 5: làm mờ, ghi chưa đủ mẫu. */
  sufficient: boolean;
}

/** Độ khớp ước lượng. brier và optimismPoints chỉ có khi sufficient (tổng n >= 15), còn lại null. */
export interface CalibrationResultDto {
  n: number;
  buckets: CalibrationBucketDto[];
  brier: number | null;
  /** Trung bình % thắng ước lượng trừ tỉ lệ thắng thật, tính bằng điểm %. Dương = lạc quan. */
  optimismPoints: number | null;
  sufficient: boolean;
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

  /**
   * Abort plan vì thesis đã sai (Vin-discipline).
   * Trả về result với status mới + danh sách trade bị ảnh hưởng.
   */
  abort(id: string, data: { trigger: InvalidationTrigger; detail: string }): Observable<AbortTradePlanResult> {
    return this.http.post<AbortTradePlanResult>(`${this.API_URL}/${id}/abort`, data, { headers: this.getHeaders() })
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

  getExpectancy(): Observable<StrategyExpectancyDto> {
    return this.http.get<StrategyExpectancyDto>(`${this.API_URL}/expectancy`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  /** Độ khớp giữa % thắng ước lượng (khoá lúc vào lệnh) và kết quả thật; tuỳ chọn theo chiến lược. */
  getCalibration(strategyId?: string): Observable<CalibrationResultDto> {
    let params = new HttpParams();
    if (strategyId) params = params.set('strategyId', strategyId);
    return this.http.get<CalibrationResultDto>(`${this.API_URL}/calibration`, { headers: this.getHeaders(), params })
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

  /** Tính lại review của MỘT kế hoạch đã review (giữ bài học, số cũ vào legacy lần đầu). */
  recomputeReview(planId: string): Observable<CampaignReviewDto> {
    return this.http.post<CampaignReviewDto>(`${this.API_URL}/${planId}/review/recompute`, {}, { headers: this.getHeaders() })
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
