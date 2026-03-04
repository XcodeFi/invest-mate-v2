import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface AppNotification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  timestamp: Date;
  read: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private notifications$ = new BehaviorSubject<AppNotification[]>([]);
  private counter = 0;

  getNotifications(): Observable<AppNotification[]> {
    return this.notifications$.asObservable();
  }

  getUnreadCount(): number {
    return this.notifications$.value.filter(n => !n.read).length;
  }

  success(title: string, message: string): void {
    this.addNotification('success', title, message);
  }

  error(title: string, message: string): void {
    this.addNotification('error', title, message);
  }

  warning(title: string, message: string): void {
    this.addNotification('warning', title, message);
  }

  info(title: string, message: string): void {
    this.addNotification('info', title, message);
  }

  markAsRead(id: string): void {
    const notifications = this.notifications$.value.map(n =>
      n.id === id ? { ...n, read: true } : n
    );
    this.notifications$.next(notifications);
  }

  markAllAsRead(): void {
    const notifications = this.notifications$.value.map(n => ({ ...n, read: true }));
    this.notifications$.next(notifications);
  }

  remove(id: string): void {
    const notifications = this.notifications$.value.filter(n => n.id !== id);
    this.notifications$.next(notifications);
  }

  clearAll(): void {
    this.notifications$.next([]);
  }

  private addNotification(type: AppNotification['type'], title: string, message: string): void {
    const notification: AppNotification = {
      id: `notif-${++this.counter}`,
      type,
      title,
      message,
      timestamp: new Date(),
      read: false
    };
    const current = this.notifications$.value;
    this.notifications$.next([notification, ...current]);
  }
}
