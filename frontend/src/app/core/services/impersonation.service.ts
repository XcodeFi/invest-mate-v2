import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, of, tap, catchError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ImpersonateRequest {
  targetUserId: string;
  reason: string;
}

export interface ImpersonateResponse {
  token: string;
  impersonationId: string;
  targetEmail: string;
  targetName: string;
  expiresAt: string;
}

export interface ImpersonationTargetInfo {
  email: string;
  name: string;
  sub: string;
}

const TOKEN_KEY = 'auth_token';
const USER_KEY = 'auth_user';
const ADMIN_TOKEN_BACKUP_KEY = 'admin_auth_token';
const ADMIN_USER_BACKUP_KEY = 'admin_auth_user';

@Injectable({ providedIn: 'root' })
export class ImpersonationService {
  private readonly API_URL = environment.apiUrl;

  constructor(private http: HttpClient, private router: Router) {}

  isImpersonating(): boolean {
    return !!localStorage.getItem(ADMIN_TOKEN_BACKUP_KEY);
  }

  getTargetInfo(): ImpersonationTargetInfo | null {
    const token = localStorage.getItem(TOKEN_KEY);
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      if (payload.amr !== 'impersonate') return null;
      return {
        email: payload.email || '',
        name: payload.name || '',
        sub: payload.sub || ''
      };
    } catch {
      return null;
    }
  }

  startImpersonate(request: ImpersonateRequest): Observable<ImpersonateResponse> {
    if (this.isImpersonating()) {
      throw new Error('Đã đang impersonate — thoát trước khi bắt đầu phiên mới');
    }

    const adminToken = localStorage.getItem(TOKEN_KEY);
    const adminUser = localStorage.getItem(USER_KEY);
    if (!adminToken) {
      throw new Error('Chưa đăng nhập');
    }

    const headers = new HttpHeaders({ Authorization: `Bearer ${adminToken}` });
    return this.http.post<ImpersonateResponse>(`${this.API_URL}/admin/impersonate`, request, { headers })
      .pipe(
        tap(response => {
          localStorage.setItem(ADMIN_TOKEN_BACKUP_KEY, adminToken);
          if (adminUser) localStorage.setItem(ADMIN_USER_BACKUP_KEY, adminUser);
          localStorage.setItem(TOKEN_KEY, response.token);
          localStorage.removeItem(USER_KEY);
        })
      );
  }

  stopImpersonate(skipApiCall = false): Observable<void> {
    const currentToken = localStorage.getItem(TOKEN_KEY);

    const restore = () => {
      const adminToken = localStorage.getItem(ADMIN_TOKEN_BACKUP_KEY);
      const adminUser = localStorage.getItem(ADMIN_USER_BACKUP_KEY);
      if (adminToken) {
        localStorage.setItem(TOKEN_KEY, adminToken);
        if (adminUser) localStorage.setItem(USER_KEY, adminUser);
      }
      localStorage.removeItem(ADMIN_TOKEN_BACKUP_KEY);
      localStorage.removeItem(ADMIN_USER_BACKUP_KEY);
    };

    if (skipApiCall || !currentToken) {
      restore();
      return of(void 0);
    }

    const headers = new HttpHeaders({ Authorization: `Bearer ${currentToken}` });
    return this.http.post(`${this.API_URL}/admin/impersonate/stop`, {}, { headers })
      .pipe(
        tap(() => restore()),
        catchError(() => {
          restore();
          return of(void 0);
        })
      ) as Observable<void>;
  }
}
