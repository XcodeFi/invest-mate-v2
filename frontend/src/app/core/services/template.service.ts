import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError, timeout } from 'rxjs';
import { catchError } from 'rxjs/operators';

export interface StrategyTemplate {
  id: string;
  name: string;
  category: string;
  description: string;
  suggestion: string;
  entryRules: string;
  exitRules: string;
  riskRules: string;
  timeFrame: string;
  marketCondition: string;
  difficultyLevel: string;
  suitableFor: string[];
  keyIndicators: string[];
  tags: string[];
  sortOrder: number;
}

export interface RiskProfileTemplate {
  id: string;
  name: string;
  description: string;
  suggestion: string;
  maxPositionSizePercent: number;
  maxSectorExposurePercent: number;
  maxDrawdownAlertPercent: number;
  defaultRiskRewardRatio: number;
  maxPortfolioRiskPercent: number;
  suitableFor: string[];
  tags: string[];
  sortOrder: number;
}

@Injectable({
  providedIn: 'root'
})
export class TemplateService {
  private readonly API_URL = `${environment.apiUrl}/templates`;

  constructor(private http: HttpClient) {}

  getStrategyTemplates(category?: string, difficulty?: string): Observable<StrategyTemplate[]> {
    let params: any = {};
    if (category) params.category = category;
    if (difficulty) params.difficulty = difficulty;
    return this.http.get<StrategyTemplate[]>(`${this.API_URL}/strategies`, { params })
      .pipe(timeout(10000), catchError(this.handleError));
  }

  getStrategyTemplate(id: string): Observable<StrategyTemplate> {
    return this.http.get<StrategyTemplate>(`${this.API_URL}/strategies/${id}`)
      .pipe(catchError(this.handleError));
  }

  getRiskProfileTemplates(): Observable<RiskProfileTemplate[]> {
    return this.http.get<RiskProfileTemplate[]>(`${this.API_URL}/risk-profiles`)
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Template API error:', error);
    return throwError(() => error);
  }
}
