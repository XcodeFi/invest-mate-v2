import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

export interface User {
  id: string;
  email: string;
  name: string;
  provider: string;
  createdAt: string;
  lastLoginAt?: string;
  avatar?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface ApiResponse<T> {
  success?: boolean;
  token?: string;
  user?: T;
  message?: string;
  error?: string;
  details?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly API_URL = environment.apiUrl;
  private tokenKey = 'auth_token';
  private userKey = 'auth_user';

  private currentUserSubject = new BehaviorSubject<User | null>(this.getSavedUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  getGoogleLoginUrl(): string {
    return `${this.API_URL}/auth/google/login`;
  }

  handleAuthCallback(code: string, state?: string): Observable<User> {
    const params = new URLSearchParams();
    if (code) params.append('code', code);
    if (state) params.append('state', state);

    return this.http.get<ApiResponse<User>>(`${this.API_URL}/auth/google/callback?${params.toString()}`)
      .pipe(
        tap(response => {
          if (response.token && response.user) {
            localStorage.setItem(this.tokenKey, response.token);
            this.saveUser(response.user);
            this.currentUserSubject.next(response.user);
          }
        }),
        catchError(error => {
          console.error('Auth callback error:', error);
          return throwError(() => error);
        })
      ) as Observable<User>;
  }

  handleAuthCallbackWithToken(token: string): Observable<User> {
    // For web redirect flow - token comes from URL parameter
    localStorage.setItem(this.tokenKey, token);

    // Decode token to get user info (simplified - in production use a proper JWT library)
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const user: User = {
        id: payload.sub,
        email: payload.email,
        name: payload.name || payload.email,
        provider: payload.provider || 'google',
        avatar: payload.avatar,
        createdAt: payload.createdAt || new Date().toISOString()
      };

      this.saveUser(user);
      this.currentUserSubject.next(user);

      return new Observable(subscriber => {
        subscriber.next(user);
        subscriber.complete();
      });
    } catch (error) {
      console.error('Token decode error:', error);
      return throwError(() => error);
    }
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_URL}/auth/login`, { email, password })
      .pipe(
        tap(response => {
          localStorage.setItem(this.tokenKey, response.token);
          this.saveUser(response.user);
          this.currentUserSubject.next(response.user);
        })
      );
  }

  getCurrentUser(): Observable<User> {
    const token = this.getToken();
    if (!token) {
      return throwError(() => new Error('No token available'));
    }

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`
    });

    return this.http.get<User>(`${this.API_URL}/auth/me`, { headers })
      .pipe(
        tap(user => {
          this.saveUser(user);
          this.currentUserSubject.next(user);
        }),
        catchError(error => {
          if (error.status === 401) {
            this.logout();
          }
          return throwError(() => error);
        })
      );
  }

  refreshToken(): Observable<AuthResponse> {
    const token = this.getToken();
    if (!token) {
      return throwError(() => new Error('No token available'));
    }

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`
    });

    return this.http.post<AuthResponse>(`${this.API_URL}/auth/refresh`, {}, { headers })
      .pipe(
        tap(response => {
          localStorage.setItem(this.tokenKey, response.token);
          this.saveUser(response.user);
          this.currentUserSubject.next(response.user);
        }),
        catchError(error => {
          if (error.status === 401) {
            this.logout();
          }
          return throwError(() => error);
        })
      );
  }

  logout(): Observable<any> {
    const token = this.getToken();
    let logoutObservable: Observable<any>;

    if (token) {
      const headers = new HttpHeaders({
        'Authorization': `Bearer ${token}`
      });
      logoutObservable = this.http.post(`${this.API_URL}/auth/logout`, {}, { headers });
    } else {
      logoutObservable = new Observable(subscriber => {
        subscriber.next({ message: 'Logged out locally' });
        subscriber.complete();
      });
    }

    return logoutObservable.pipe(
      tap(() => {
        localStorage.removeItem(this.tokenKey);
        localStorage.removeItem(this.userKey);
        this.currentUserSubject.next(null);
        this.router.navigate(['/auth/login']);
      }),
      catchError(error => {
        // Even if API call fails, clear local storage
        localStorage.removeItem(this.tokenKey);
        localStorage.removeItem(this.userKey);
        this.currentUserSubject.next(null);
        this.router.navigate(['/auth/login']);
        return throwError(() => error);
      })
    );
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const expiry = payload.exp * 1000; // Convert to milliseconds
      return Date.now() < expiry;
    } catch {
      return false;
    }
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getCurrentUserValue(): User | null {
    return this.currentUserSubject.value;
  }

  private getSavedUser(): User | null {
    const userJson = localStorage.getItem(this.userKey);
    return userJson ? JSON.parse(userJson) : null;
  }

  private saveUser(user: User): void {
    localStorage.setItem(this.userKey, JSON.stringify(user));
  }
}