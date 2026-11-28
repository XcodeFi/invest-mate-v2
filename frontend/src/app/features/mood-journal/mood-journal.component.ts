import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MoodDayDto, MoodHistoryDto, MoodService, MoodState } from '../../core/services/mood.service';
import { MOOD_LABELS } from '../dashboard/widgets/patience-quotes';
import {
  Cell, MOOD_MARK, MoodCounts, UNRATED_MARK, buildCells, cellLabel, changeLine, countByMood, emotionalDays, formatDateKey, leadingBlanks
} from './mood-journal-model';

/**
 * Nhật ký tâm trạng (ADR-0013 phụ lục): lịch 90 ngày theo ngày VN, mỗi ô một tâm trạng cuối ngày bằng MÀU kèm BIỂU TƯỢNG riêng
 * (■ Bình tĩnh, ▲ FOMO, ▼ Sợ, ✕ Cay cú, □ chưa chấm), số ngày theo từng tâm trạng, danh sách ngày đặt lệnh khi đang cảm xúc, và nút "Xem bảng"
 * cho cùng số liệu. Mọi con số (cờ cảm xúc, số lệnh) do server tính; trang chỉ hiển thị. Chữ luôn dùng màu chữ, màu danh tính chỉ ở ô và chú giải.
 */
@Component({
  selector: 'app-mood-journal',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="mj-root max-w-5xl mx-auto px-4 py-6">
      <div class="flex flex-wrap items-baseline justify-between gap-2 mb-2">
        <h1 class="text-2xl font-bold text-gray-800">Nhật ký tâm trạng</h1>
        <a routerLink="/dashboard" class="text-sm text-blue-600 underline">← Trang chủ</a>
      </div>
      <p class="text-sm text-gray-600 mb-4">
        Mỗi ô là một ngày (giờ Việt Nam), 90 ngày gần nhất. Ô ghi tâm trạng cuối ngày bạn đã chấm; □ là ngày chưa chấm.
        Tâm trạng do trợ lý ghi được đánh dấu "từ agent".
      </p>

      @if (loading) {
        <p class="text-sm text-gray-500" data-testid="mj-loading">Đang tải...</p>
      } @else if (error) {
        <p class="text-sm text-red-600" role="alert" data-testid="mj-error">{{ error }}</p>
      } @else if (history) {
        <div class="bg-white rounded-lg shadow p-4 mb-4">
          <div class="flex items-center justify-between gap-2 mb-3">
            <h2 class="text-lg font-semibold text-gray-700">Lịch tâm trạng</h2>
            <button type="button" data-testid="mj-table-toggle" class="text-xs text-gray-600 underline" [attr.aria-pressed]="showTable"
              (click)="toggleTable()">{{ showTable ? 'Xem biểu đồ' : 'Xem bảng' }}</button>
          </div>

          @if (!showTable) {
            @if (history.days.length === 0) {
              <p class="text-sm text-gray-500 mb-2" data-testid="mj-empty">Chưa có ngày nào được chấm trong khoảng này.</p>
            }
            <div class="overflow-x-auto" data-testid="mj-scroller" #scroller>
              <div class="mj-grid" data-testid="mj-grid" role="group" aria-label="Lịch tâm trạng 90 ngày">
                @for (i of blanks; track i) { <span class="mj-blank" aria-hidden="true"></span> }
                @for (c of cells; track c.dateKey) {
                  <button type="button" class="mj-cell" data-testid="mj-cell" [attr.data-date]="c.dateKey" [attr.data-mood]="c.mood ?? 'none'"
                    [class]="'mj-cell ' + cellClass(c)" tabindex="0" [attr.aria-label]="label(c)" [attr.title]="label(c)" (click)="select(c)">
                    <span data-testid="mj-glyph" aria-hidden="true">{{ glyph(c) }}</span>
                  </button>
                }
              </div>
            </div>

            @if (selected; as s) {
              <p class="mt-3 text-sm text-gray-700" data-testid="mj-detail">
                <strong>{{ formatDate(s.dateKey) }}</strong>: {{ moodText(s) }}
                @if (s.day && s.day.changes.length > 0) {
                  · các lần đổi: {{ changesText(s.day) }}
                }
                @if (s.day && s.day.tradeCount > 0) { · {{ s.day.tradeCount }} lệnh }
                @if (s.day?.overrode) { · đã bấm qua lớp phủ }
              </p>
            }

            <ul class="mj-legend mt-3" data-testid="mj-legend">
              @for (m of moodKeys; track m) {
                <li><span class="mj-key" [class]="'mj-key ' + keyClass(m)" aria-hidden="true">{{ marks[m].glyph }}</span> {{ marks[m].label }}
                  <strong [attr.data-testid]="'mj-count-' + m">{{ counts[m] }}</strong></li>
              }
              <li><span class="mj-key mj-key-none" aria-hidden="true">{{ unrated.glyph }}</span> {{ unrated.label }}
                <strong data-testid="mj-count-unrated">{{ counts.unrated }}</strong></li>
            </ul>
          } @else {
            <div class="overflow-x-auto">
              <table class="min-w-full text-sm" data-testid="mj-table">
                <thead>
                  <tr class="text-left text-gray-500 border-b"><th class="py-1 pr-3">Ngày</th><th class="py-1 pr-3">Tâm trạng</th><th class="py-1 pr-3 text-right">Số lần đổi</th><th class="py-1 pr-3 text-right">Số lệnh</th><th class="py-1">Đặt lệnh khi cảm xúc</th></tr>
                </thead>
                <tbody>
                  @for (d of history.days; track d.dateKey) {
                    <tr class="border-b" data-testid="mj-table-row" [attr.data-date]="d.dateKey">
                      <td class="py-1 pr-3">{{ formatDate(d.dateKey) }}</td>
                      <td class="py-1 pr-3">{{ d.mood ? labels[d.mood] : unrated.label }}</td>
                      <td class="py-1 pr-3 text-right">{{ d.changes.length }}</td>
                      <td class="py-1 pr-3 text-right">{{ d.tradeCount }}</td>
                      <td class="py-1">{{ d.emotionalTrade ? 'Có' : 'Không' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>

        <div class="bg-white rounded-lg shadow p-4">
          <h2 class="text-lg font-semibold text-gray-700 mb-2">Ngày đặt lệnh khi đang cảm xúc</h2>
          @if (emotional.length === 0) {
            <p class="text-sm text-gray-500" data-testid="mj-emotional-empty">Không có ngày nào trong khoảng này.</p>
          } @else {
            <ul class="text-sm text-gray-700 divide-y">
              @for (d of emotional; track d.dateKey) {
                <li class="py-1 flex flex-wrap items-baseline gap-x-3" data-testid="mj-emotional-row" [attr.data-date]="d.dateKey">
                  <span class="font-medium">{{ formatDate(d.dateKey) }}</span>
                  <span>{{ d.mood ? labels[d.mood] : unrated.label }}</span>
                  <span>{{ d.tradeCount }} lệnh</span>
                  @if (d.overrode) { <span class="text-gray-500">đã bấm qua lớp phủ</span> }
                  <a routerLink="/trades" class="text-blue-600 underline ml-auto">Xem giao dịch →</a>
                </li>
              }
            </ul>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    /* Bốn hue danh tính đầu của bảng màu dataviz mặc định theo thứ tự cố định (xanh, cam, ngọc, vàng), chạy validate_palette.js ở cả hai chế độ:
       sáng (nền #ffffff): đạt dải sáng, độ bão hoà, tách CVD (ΔE 11,8) và nhìn thường (23,8); cam/ngọc/vàng dưới 3:1 nên BẮT BUỘC có biểu tượng,
       chú giải và bảng. Tối (nền #111827): đạt tất cả kể cả tương phản ≥ 3:1 (CVD ΔE 11,1; nhìn thường 21,1). */
    :host { --mj-1: #257de0; --mj-2: #f47230; --mj-3: #00b894; --mj-4: #f2a600; --mj-on-1: #ffffff; --mj-on-2: #0b0b0b; --mj-on-3: #0b0b0b; --mj-on-4: #0b0b0b;
      --mj-none-border: #9ca3af; --mj-none-ink: #6b7280; --mj-ink: #191919; --mj-ink-2: #666666; }
    :host-context(html.dark) { --mj-1: #338ced; --mj-2: #e76624; --mj-3: #00a888; --mj-4: #d28e05; --mj-on-1: #0b0b0b;
      --mj-none-border: #64748b; --mj-none-ink: #94a3b8; --mj-ink: #e5e7eb; --mj-ink-2: #94a3b8; }
    .mj-grid { display: grid; grid-template-rows: repeat(7, 28px); grid-auto-flow: column; grid-auto-columns: 28px; gap: 3px; width: max-content; }
    .mj-cell, .mj-blank { width: 28px; height: 28px; }
    .mj-cell { display: inline-flex; align-items: center; justify-content: center; border-radius: 4px; font-size: 13px; font-weight: 700; line-height: 1; cursor: pointer; border: 1px solid transparent; }
    .mj-cell:focus-visible { outline: 2px solid var(--mj-ink); outline-offset: 1px; }
    .mj-m-Calm { background: var(--mj-1); color: var(--mj-on-1); }
    .mj-m-Fomo { background: var(--mj-2); color: var(--mj-on-2); }
    .mj-m-Fear { background: var(--mj-3); color: var(--mj-on-3); }
    .mj-m-Revenge { background: var(--mj-4); color: var(--mj-on-4); }
    .mj-m-none { background: transparent; color: var(--mj-none-ink); border-color: var(--mj-none-border); }
    .mj-legend { display: flex; flex-wrap: wrap; gap: 0.25rem 1rem; font-size: 0.75rem; color: var(--mj-ink); list-style: none; padding: 0; }
    .mj-key { display: inline-flex; width: 16px; height: 16px; align-items: center; justify-content: center; border-radius: 3px; font-size: 10px; font-weight: 700; margin-right: 2px; vertical-align: -3px; }
    .mj-key-Calm { background: var(--mj-1); color: var(--mj-on-1); }
    .mj-key-Fomo { background: var(--mj-2); color: var(--mj-on-2); }
    .mj-key-Fear { background: var(--mj-3); color: var(--mj-on-3); }
    .mj-key-Revenge { background: var(--mj-4); color: var(--mj-on-4); }
    .mj-key-none { border: 1px solid var(--mj-none-border); color: var(--mj-none-ink); }
  `]
})
export class MoodJournalComponent implements OnInit {
  readonly moodKeys: MoodState[] = ['Calm', 'Fomo', 'Fear', 'Revenge'];
  readonly marks = MOOD_MARK;
  readonly unrated = UNRATED_MARK;
  readonly labels = MOOD_LABELS as Record<MoodState, string>;

  loading = true;
  error = '';
  history: MoodHistoryDto | null = null;
  cells: Cell[] = [];
  blanks: number[] = [];
  counts: MoodCounts = { Calm: 0, Fomo: 0, Fear: 0, Revenge: 0, unrated: 0 };
  emotional: MoodDayDto[] = [];
  showTable = false;
  selected: Cell | null = null;
  @ViewChild('scroller') scroller?: ElementRef<HTMLElement>;

  constructor(private moodService: MoodService) {}

  ngOnInit(): void {
    this.moodService.getHistory().subscribe({
      next: h => {
        this.history = h;
        this.cells = buildCells(h);
        this.blanks = Array.from({ length: leadingBlanks(this.cells) }, (_, i) => i);
        this.counts = countByMood(this.cells);
        this.emotional = emotionalDays(h);
        this.loading = false;
        // Màn hẹp: mở ra ở tuần mới nhất (hôm nay ở mép phải), không phải 90 ngày trước.
        this.scrollToLatest();
      },
      error: () => {
        this.loading = false;
        this.error = 'Không tải được lịch sử tâm trạng, thử lại sau.';
      }
    });
  }

  /** Màn hẹp: lịch mở ở tuần mới nhất (hôm nay ở mép phải). Gọi lại mỗi khi lịch được dựng lại (sau khi bật tắt bảng). */
  private scrollToLatest(): void {
    setTimeout(() => { const el = this.scroller?.nativeElement; if (el) el.scrollLeft = el.scrollWidth; });
  }

  toggleTable(): void {
    this.showTable = !this.showTable;
    if (!this.showTable) this.scrollToLatest();
  }

  cellClass(c: Cell): string { return 'mj-m-' + (c.mood ?? 'none'); }
  keyClass(m: MoodState): string { return 'mj-key-' + m; }
  glyph(c: Cell): string { return c.mood ? MOOD_MARK[c.mood].glyph : UNRATED_MARK.glyph; }
  label(c: Cell): string { return cellLabel(c); }
  select(c: Cell): void { this.selected = c; }
  formatDate(key: string): string { return formatDateKey(key); }
  moodText(c: Cell): string { return c.mood ? MOOD_LABELS[c.mood] : 'chưa chấm'; }
  changesText(day: MoodDayDto): string { return day.changes.map(changeLine).join(', '); }
}
