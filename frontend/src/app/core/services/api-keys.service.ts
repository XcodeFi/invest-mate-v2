import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

/** Danh sách khóa — không bao giờ kèm token gốc hay hash. */
export interface ApiKeyDto {
  id: string;
  name: string;
  prefix: string;
  createdAt: string;
  expiresAt: string;
  lastUsedAt?: string | null;
  revokedAt?: string | null;
  isActive: boolean;
}

export interface CreateApiKeyRequest {
  name: string;
  expiresInDays: number;
}

/** Trả về một lần duy nhất khi tạo. `token` là plaintext, không lấy lại được. */
export interface CreatedApiKeyDto {
  id: string;
  name: string;
  prefix: string;
  token: string;
  expiresAt: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ApiKeysService {
  private URL = `${environment.apiUrl}/api-keys`;
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  list(): Observable<ApiKeyDto[]> {
    return this.http.get<ApiKeyDto[]>(this.URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  create(req: CreateApiKeyRequest): Observable<CreatedApiKeyDto> {
    return this.http.post<CreatedApiKeyDto>(this.URL, req, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  revoke(id: string): Observable<void> {
    return this.http.delete<void>(`${this.URL}/${id}`, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('API keys error:', error);
    return throwError(() => error);
  }
}
