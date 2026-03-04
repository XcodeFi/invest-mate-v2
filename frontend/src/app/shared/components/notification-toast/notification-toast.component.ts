import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, AppNotification } from '../../../core/services/notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-notification-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="fixed top-4 right-4 z-50 space-y-3 max-w-sm">
      <div *ngFor="let notification of visibleNotifications; trackBy: trackById"
        class="rounded-lg shadow-lg border p-4 transition-all duration-300 ease-in-out"
        [class]="getNotificationClasses(notification)">
        <div class="flex items-start">
          <div class="flex-shrink-0">
            <!-- Success Icon -->
            <svg *ngIf="notification.type === 'success'" class="w-5 h-5 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>
            <!-- Error Icon -->
            <svg *ngIf="notification.type === 'error'" class="w-5 h-5 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>
            <!-- Warning Icon -->
            <svg *ngIf="notification.type === 'warning'" class="w-5 h-5 text-yellow-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"></path>
            </svg>
            <!-- Info Icon -->
            <svg *ngIf="notification.type === 'info'" class="w-5 h-5 text-blue-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>
          </div>
          <div class="ml-3 flex-1">
            <p class="text-sm font-medium">{{ notification.title }}</p>
            <p class="text-sm mt-1 opacity-80">{{ notification.message }}</p>
          </div>
          <button (click)="dismiss(notification.id)" class="ml-3 flex-shrink-0 opacity-60 hover:opacity-100">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
            </svg>
          </button>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class NotificationToastComponent implements OnInit, OnDestroy {
  visibleNotifications: AppNotification[] = [];
  private subscription?: Subscription;
  private autoRemoveTimers = new Map<string, any>();

  constructor(private notificationService: NotificationService) {}

  ngOnInit(): void {
    this.subscription = this.notificationService.getNotifications().subscribe(notifications => {
      // Show only latest 5 unread
      this.visibleNotifications = notifications.filter(n => !n.read).slice(0, 5);

      // Set auto-dismiss for new notifications
      this.visibleNotifications.forEach(n => {
        if (!this.autoRemoveTimers.has(n.id)) {
          const timer = setTimeout(() => {
            this.dismiss(n.id);
          }, 5000);
          this.autoRemoveTimers.set(n.id, timer);
        }
      });
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.autoRemoveTimers.forEach(timer => clearTimeout(timer));
  }

  dismiss(id: string): void {
    this.notificationService.markAsRead(id);
    const timer = this.autoRemoveTimers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.autoRemoveTimers.delete(id);
    }
  }

  getNotificationClasses(notification: AppNotification): string {
    switch (notification.type) {
      case 'success': return 'bg-green-50 border-green-200 text-green-800';
      case 'error': return 'bg-red-50 border-red-200 text-red-800';
      case 'warning': return 'bg-yellow-50 border-yellow-200 text-yellow-800';
      case 'info': return 'bg-blue-50 border-blue-200 text-blue-800';
      default: return 'bg-gray-50 border-gray-200 text-gray-800';
    }
  }

  trackById(index: number, item: AppNotification): string {
    return item.id;
  }
}
