import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface TradePlanTemplate {
  id: string;
  userId: string;
  name: string;
  symbol?: string;
  direction: 'Buy' | 'Sell';
  entryPrice?: number;
  stopLoss?: number;
  target?: number;
  strategyId?: string;
  marketCondition: string;
  reason?: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTradePlanTemplateRequest {
  name: string;
  symbol?: string;
  direction?: string;
  entryPrice?: number;
  stopLoss?: number;
  target?: number;
  strategyId?: string;
  marketCondition?: string;
  reason?: string;
  notes?: string;
}

@Injectable({
  providedIn: 'root'
})
export class TradePlanTemplateService {
  private readonly API_URL = `${environment.apiUrl}/templates/trade-plans`;

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

  getAll(): Observable<TradePlanTemplate[]> {
    return this.http.get<TradePlanTemplate[]>(this.API_URL, { headers: this.getHeaders() })
      .pipe(catchError(err => throwError(() => err)));
  }

  create(request: CreateTradePlanTemplateRequest): Observable<TradePlanTemplate> {
    return this.http.post<TradePlanTemplate>(this.API_URL, request, { headers: this.getHeaders() })
      .pipe(catchError(err => throwError(() => err)));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(err => throwError(() => err)));
  }
}
