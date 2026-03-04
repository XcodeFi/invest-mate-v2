import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface AlertRule {
  id: string;
  name: string;
  alertType: string;
  condition: string;
  threshold: number;
  symbol: string | null;
  portfolioId: string | null;
  channel: string;
  isActive: boolean;
  lastTriggeredAt: string | null;
  createdAt: string;
}

export interface CreateAlertRuleRequest {
  name: string;
  alertType: string;
  condition: string;
  threshold: number;
  symbol?: string;
  portfolioId?: string;
  channel?: string;
}

export interface UpdateAlertRuleRequest {
  name?: string;
  alertType?: string;
  condition?: string;
  threshold?: number;
  symbol?: string;
  portfolioId?: string;
  channel?: string;
  isActive?: boolean;
}

export interface AlertHistoryItem {
  id: string;
  alertRuleId: string;
  alertType: string;
  title: string;
  message: string;
  portfolioId: string | null;
  symbol: string | null;
  currentValue: number | null;
  thresholdValue: number | null;
  isRead: boolean;
  triggeredAt: string;
}

export interface AlertHistoryResult {
  alerts: AlertHistoryItem[];
  unreadCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class AlertService {
  private readonly API_URL = `${environment.apiUrl}/alerts`;

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

  getRules(): Observable<AlertRule[]> {
    return this.http.get<AlertRule[]>(`${this.API_URL}/rules`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  createRule(data: CreateAlertRuleRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.API_URL}/rules`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  updateRule(id: string, data: UpdateAlertRuleRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/rules/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  deleteRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/rules/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getHistory(unreadOnly: boolean = false): Observable<AlertHistoryResult> {
    let params = new HttpParams();
    if (unreadOnly) {
      params = params.set('unreadOnly', 'true');
    }
    return this.http.get<AlertHistoryResult>(`${this.API_URL}/history`, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  markAsRead(id: string): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}/read`, {}, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Alert API error:', error);
    return throwError(() => error);
  }
}

