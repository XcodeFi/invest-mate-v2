import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './shared/components/header/header.component';
import { NotificationToastComponent } from './shared/components/notification-toast/notification-toast.component';
import { BottomNavComponent } from './shared/components/bottom-nav/bottom-nav.component';
import { SidebarComponent } from './shared/components/sidebar/sidebar.component';
import { PwaInstallBannerComponent } from './shared/components/pwa-install-banner/pwa-install-banner.component';
import { ImpersonationBannerComponent } from './shared/components/impersonation-banner/impersonation-banner.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, NotificationToastComponent, BottomNavComponent, PwaInstallBannerComponent, ImpersonationBannerComponent],
  template: `
    <div class="min-h-screen bg-gray-50">
      <app-impersonation-banner></app-impersonation-banner>
      <app-header></app-header>
      <div class="lg:flex">
        <app-sidebar></app-sidebar>
        <main class="flex-1 min-w-0 pb-14 md:pb-0">
          <router-outlet></router-outlet>
        </main>
      </div>
      <app-bottom-nav></app-bottom-nav>
      <app-notification-toast></app-notification-toast>
      <app-pwa-install-banner></app-pwa-install-banner>
    </div>
  `,
  styles: []
})
export class AppComponent {
  title = 'Investment Mate v2';
}