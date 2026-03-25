import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './shared/components/header/header.component';
import { NotificationToastComponent } from './shared/components/notification-toast/notification-toast.component';
import { BottomNavComponent } from './shared/components/bottom-nav/bottom-nav.component';
import { PwaInstallBannerComponent } from './shared/components/pwa-install-banner/pwa-install-banner.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, NotificationToastComponent, BottomNavComponent, PwaInstallBannerComponent],
  template: `
    <div class="min-h-screen bg-gray-50">
      <app-header></app-header>
      <main class="pb-14 md:pb-0">
        <router-outlet></router-outlet>
      </main>
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