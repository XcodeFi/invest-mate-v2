import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface AdminUserDto {
  id: string;
  email: string;
  name: string;
  role: string;
  createdAt: string;
}

export interface UserOverviewDto {
  id: string;
  email: string;
  name: string;
  role: string;
  createdAt: string;
  lastLoginAt: string | null;
  portfolioCount: number;
  tradeCount: number;
  lastTradeAt: string | null;
  lastImpersonatedAt: string | null;
}

export interface UsersOverviewResult {
  items: UserOverviewDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly API_URL = environment.apiUrl;

  constructor(private http: HttpClient, private authService: AuthService) {}

  private authHeaders(): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${this.authService.getToken()}` });
  }

  searchUsers(email: string): Observable<AdminUserDto[]> {
    const params = new HttpParams().set('email', email ?? '');
    return this.http.get<AdminUserDto[]>(`${this.API_URL}/admin/users`, {
      headers: this.authHeaders(),
      params
    });
  }

  getUsersOverview(page: number = 1, pageSize: number = 20): Observable<UsersOverviewResult> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<UsersOverviewResult>(`${this.API_URL}/admin/users/overview`, {
      headers: this.authHeaders(),
      params
    });
  }
}
