import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withInterceptorsFromDi } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { isDevMode, LOCALE_ID } from '@angular/core';
import { provideServiceWorker } from '@angular/service-worker';
import { registerLocaleData } from '@angular/common';
import localeVi from '@angular/common/locales/vi';
import localeViExtra from '@angular/common/locales/extra/vi';

import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { impersonationRevokedInterceptor } from './app/core/interceptors/impersonation-revoked.interceptor';

registerLocaleData(localeVi, 'vi-VN', localeViExtra);

bootstrapApplication(AppComponent, {
  providers: [
    { provide: LOCALE_ID, useValue: 'vi-VN' },
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([impersonationRevokedInterceptor]),
      withInterceptorsFromDi()
    ),
    provideAnimationsAsync(),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000'
    })
  ]
}).catch(err => console.error(err));