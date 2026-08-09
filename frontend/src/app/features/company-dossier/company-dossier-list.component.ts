import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CompanyDossierService, CompanyDossierDto } from '../../core/services/company-dossier.service';

@Component({
  selector: 'app-company-dossier-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Hồ sơ công ty</h1>

      <div *ngIf="loading" class="text-center text-gray-400 py-10">Đang tải...</div>

      <div *ngIf="!loading" class="bg-white rounded-lg shadow overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="bg-gray-50 text-xs text-gray-500 uppercase">
            <tr>
              <th class="px-4 py-2 text-left">Mã CK</th>
              <th class="px-4 py-2 text-center">Trạng thái tươi</th>
              <th class="px-4 py-2 text-center">Số yếu tố rủi ro</th>
              <th class="px-4 py-2 text-left">Soát gần nhất</th>
              <th class="px-4 py-2 text-center">Thao tác</th>
            </tr>
          </thead>
          <tbody class="divide-y">
            <tr *ngFor="let d of dossiers" class="hover:bg-gray-50">
              <td class="px-4 py-2 font-bold">{{ d.symbol }}</td>
              <td class="px-4 py-2 text-center">
                <span class="px-2 py-0.5 rounded-full text-xs font-medium" [ngClass]="freshnessClass(d.freshness)">
                  {{ freshnessLabel(d.freshness) }}
                </span>
              </td>
              <td class="px-4 py-2 text-center">{{ d.riskFactors.length }}</td>
              <td class="px-4 py-2 text-gray-500">{{ d.reviewedAt | date:'short' }}</td>
              <td class="px-4 py-2 text-center">
                <a [routerLink]="['/company-dossier', d.symbol]" class="text-blue-600 hover:underline text-xs font-medium">Xem chi tiết</a>
              </td>
            </tr>
          </tbody>
        </table>
        <div *ngIf="dossiers.length === 0" class="px-4 py-8 text-center text-gray-400 text-sm">Chưa có hồ sơ công ty nào</div>
      </div>
    </div>
  `,
})
export class CompanyDossierListComponent implements OnInit {
  dossiers: CompanyDossierDto[] = [];
  loading = false;

  constructor(private dossierService: CompanyDossierService) {}

  ngOnInit(): void {
    this.loading = true;
    this.dossierService.list().subscribe({
      next: (data) => {
        this.dossiers = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  freshnessLabel(freshness: string): string {
    switch (freshness) {
      case 'Fresh': return 'Còn mới';
      case 'NeedsReview': return 'Cần soát lại';
      case 'Expired': return 'Đã hết hạn';
      default: return 'Chưa xác nhận';
    }
  }

  freshnessClass(freshness: string): Record<string, boolean> {
    return {
      'bg-emerald-100 text-emerald-700': freshness === 'Fresh',
      'bg-yellow-100 text-yellow-700': freshness === 'NeedsReview',
      'bg-red-100 text-red-700': freshness === 'Expired',
      'bg-gray-100 text-gray-600': freshness === 'Unconfirmed',
    };
  }
}
