import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface FeeCalculationRequest {
  symbol: string;
  tradeType: string;
  quantity: number;
  price: number;
}

export interface FeeCalculationResponse {
  transactionFee: number;
  tax: number;
  vat: number;
  totalFees: number;
  breakdown: {
    transactionFee: number;
    tax: number;
    vat: number;
  };
}

export interface CustodyFeeRequest {
  portfolioValue: number;
  months: number;
}

export interface CustodyFeeResponse {
  monthlyFee: number;
  totalFee: number;
  breakdown: {
    monthlyFee: number;
    totalFee: number;
  };
}

export interface TransferFeeRequest {
  symbol: string;
  quantity: number;
}

export interface TransferFeeResponse {
  transferFee: number;
  breakdown: {
    transferFee: number;
  };
}

@Injectable({
  providedIn: 'root'
})
export class FeeService {
  private readonly API_URL = `${environment.apiUrl}/fees`;

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

  calculateFees(request: FeeCalculationRequest): Observable<FeeCalculationResponse> {
    return this.http.post<FeeCalculationResponse>(`${this.API_URL}/calculate`, request, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  calculateCustodyFees(request: CustodyFeeRequest): Observable<CustodyFeeResponse> {
    return this.http.post<CustodyFeeResponse>(`${this.API_URL}/custody`, request, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  calculateTransferFees(request: TransferFeeRequest): Observable<TransferFeeResponse> {
    return this.http.post<TransferFeeResponse>(`${this.API_URL}/transfer`, request, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  getFeeConfig(): Observable<any> {
    return this.http.get(`${this.API_URL}/config`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Fee API error:', error);
    return throwError(() => error);
  }
}