import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './shared/components/header/header.component';
import { NotificationToastComponent } from './shared/components/notification-toast/notification-toast.component';
import { BottomNavComponent } from './shared/components/bottom-nav/bottom-nav.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, NotificationToastComponent, BottomNavComponent],
  template: `
    <div class="min-h-screen bg-gray-50">
      <app-header></app-header>
      <main class="pb-14 md:pb-0">
        <router-outlet></router-outlet>
      </main>
      <app-bottom-nav></app-bottom-nav>
      <app-notification-toast></app-notification-toast>
    </div>
  `,
  styles: []
})
export class AppComponent {
  title = 'Investment Mate v2';
}