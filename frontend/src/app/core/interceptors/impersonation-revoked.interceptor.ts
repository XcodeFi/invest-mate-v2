import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ImpersonationService } from '../services/impersonation.service';
import { NotificationService } from '../services/notification.service';

export const impersonationRevokedInterceptor: HttpInterceptorFn = (req, next) => {
  const impersonationService = inject(ImpersonationService);
  const router = inject(Router);
  const notifications = inject(NotificationService, { optional: true });

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        const revoked = error.headers.get('X-Impersonation-Revoked');
        if (revoked === 'true' && impersonationService.isImpersonating()) {
          impersonationService.stopImpersonate(true).subscribe();
          notifications?.warning('Phiên impersonate kết thúc', 'Đã khôi phục phiên admin');
          router.navigate(['/dashboard']);
        }
      }
      return throwError(() => error);
    })
  );
};
