import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';

export interface RoutineItem {
  index: number;
  label: string;
  group: string; // "Sáng" | "Trong phiên" | "Cuối ngày"
  link?: string;
  isRequired: boolean;
  isCompleted: boolean;
  completedAt?: string;
  note?: string;
  emoji?: string;
}

export interface DailyRoutine {
  id: string;
  date: string;
  templateId: string;
  templateName: string;
  items: RoutineItem[];
  completedCount: number;
  totalCount: number;
  progressPercent: number;
  isFullyCompleted: boolean;
  currentStreak: number;
  longestStreak: number;
  completedAt?: string;
}

export interface RoutineItemTemplate {
  index: number;
  label: string;
  group: string;
  link?: string;
  isRequired: boolean;
  emoji?: string;
}

export interface RoutineTemplate {
  id: string;
  name: string;
  description?: string;
  emoji: string;
  category: string;
  estimatedMinutes: number;
  isOneTime: boolean;
  isUrgent: boolean;
  items: RoutineItemTemplate[];
  isBuiltIn: boolean;
  isSuggested: boolean;
}

export interface RoutineHistoryDay {
  date: string;
  templateName: string;
  isCompleted: boolean;
  completedCount: number;
  totalCount: number;
}

export interface RoutineHistory {
  currentStreak: number;
  longestStreak: number;
  days: RoutineHistoryDay[];
}

@Injectable({
  providedIn: 'root'
})
export class DailyRoutineService {
  private readonly API_URL = `${environment.apiUrl}/daily-routines`;

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

  private getLocalDate(): string {
    return new Date().toISOString().split('T')[0];
  }

  getToday(): Observable<DailyRoutine | null> {
    const params = new HttpParams().set('localDate', this.getLocalDate());
    return this.http.get<DailyRoutine | null>(this.API_URL, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  getOrCreateToday(templateId?: string): Observable<DailyRoutine> {
    const body: any = { localDate: this.getLocalDate() };
    if (templateId) body.templateId = templateId;
    return this.http.post<DailyRoutine>(this.API_URL, body, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  completeItem(id: string, index: number, isCompleted: boolean): Observable<DailyRoutine> {
    return this.http.patch<DailyRoutine>(
      `${this.API_URL}/${id}/items/${index}`,
      { isCompleted },
      { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  switchTemplate(templateId: string): Observable<DailyRoutine> {
    return this.http.post<DailyRoutine>(
      `${this.API_URL}/switch-template`,
      { templateId, localDate: this.getLocalDate() },
      { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  getHistory(days: number = 30): Observable<RoutineHistory> {
    const params = new HttpParams().set('days', days.toString());
    return this.http.get<RoutineHistory>(`${this.API_URL}/history`, { headers: this.getHeaders(), params })
      .pipe(catchError(this.handleError));
  }

  getTemplates(): Observable<RoutineTemplate[]> {
    return this.http.get<RoutineTemplate[]>(`${this.API_URL}/templates`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getSuggestedTemplate(): Observable<RoutineTemplate | null> {
    const params = new HttpParams().set('localDate', this.getLocalDate());
    return this.http.get<RoutineTemplate | null>(
      `${this.API_URL}/templates/suggest`,
      { headers: this.getHeaders(), params }
    ).pipe(catchError(this.handleError));
  }

  createTemplate(data: {
    name: string;
    description?: string;
    emoji: string;
    estimatedMinutes: number;
    items: RoutineItemTemplate[];
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.API_URL}/templates`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  updateTemplate(id: string, data: {
    name?: string;
    description?: string;
    emoji?: string;
    estimatedMinutes?: number;
    items?: RoutineItemTemplate[];
  }): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/templates/${id}`, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  deleteTemplate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/templates/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('DailyRoutine API error:', error);
    return throwError(() => error);
  }
}
