import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({ providedIn: 'root' })
export class AdminGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/auth/login']);
      return false;
    }

    const token = this.authService.getToken();
    if (!token) {
      this.router.navigate(['/auth/login']);
      return false;
    }

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      if (payload.role !== 'Admin') {
        this.router.navigate(['/dashboard']);
        return false;
      }
      // Block access when already impersonating — admin actions require original admin token
      if (payload.amr === 'impersonate') {
        this.router.navigate(['/dashboard']);
        return false;
      }
      return true;
    } catch {
      this.router.navigate(['/auth/login']);
      return false;
    }
  }
}
