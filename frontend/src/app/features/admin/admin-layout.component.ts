import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface AdminMenuItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="flex flex-col md:flex-row min-h-[calc(100vh-4rem)] bg-gray-50">
      <aside class="w-full md:w-56 shrink-0 border-b md:border-b-0 md:border-r bg-white">
        <div class="px-4 py-3 border-b">
          <div class="text-xs font-semibold text-red-600 tracking-wider">ADMIN</div>
          <div class="text-sm text-gray-600 mt-0.5">Công cụ quản trị</div>
        </div>
        <nav class="p-2 space-y-1">
          <a *ngFor="let item of menu"
             [routerLink]="item.route"
             routerLinkActive="bg-red-50 text-red-700 border-red-200"
             [routerLinkActiveOptions]="{ exact: false }"
             class="flex items-center gap-2 px-3 py-2 text-sm rounded border border-transparent text-gray-700 hover:bg-gray-50">
            <span class="text-base" aria-hidden="true">{{ item.icon }}</span>
            <span class="truncate">{{ item.label }}</span>
          </a>
        </nav>
      </aside>
      <main class="flex-1 min-w-0">
        <router-outlet></router-outlet>
      </main>
    </div>
  `
})
export class AdminLayoutComponent {
  menu: AdminMenuItem[] = [
    { label: 'Tổng quan user', icon: '📊', route: 'users/overview' },
    { label: 'Tìm & Impersonate', icon: '🔍', route: 'users/search' },
  ];
}
