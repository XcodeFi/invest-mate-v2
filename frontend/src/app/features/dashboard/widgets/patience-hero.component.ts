import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { catchError, of } from 'rxjs';
import { MoodService, MoodState, TodayMoodDto } from '../../../core/services/mood.service';
import { TradeService } from '../../../core/services/trade.service';
import { FishingSceneComponent } from './fishing-scene.component';
import { MOOD_LABELS, MoodKey, Quote, pickQuote } from './patience-quotes';

/** Trần dùng cho hình ảnh. Số ngày HIỂN THỊ luôn là số thật, không bị cắt ở đây. */
const CALM_CEILING_DAYS = 14;

/**
 * Màn tĩnh tâm đầu trang chủ (ADR-0013): một khoảng lặng giữa lúc mở app và lúc bấm
 * vào một hành động. Cảnh câu bám theo số ngày chưa đặt lệnh, châm ngôn theo tâm trạng
 * người dùng tự chấm, và phát `moodChange` để trang chủ phủ mờ Hàng đợi quyết định.
 */
@Component({
  selector: 'app-patience-hero',
  standalone: true,
  imports: [CommonModule, FishingSceneComponent],
  template: `
    <div class="bg-white rounded-xl shadow-sm border border-gray-200 mb-6 overflow-hidden">
      <app-fishing-scene [calm]="calm" [dim]="isEmotional"></app-fishing-scene>

      <div class="px-6 py-5 text-center">
        <blockquote class="text-base sm:text-lg text-gray-800 leading-relaxed max-w-2xl mx-auto">
          <span>{{ quote.text }}</span>
        </blockquote>
        <p *ngIf="quote.author" class="mt-1 text-sm text-gray-500">— {{ quote.author }}</p>

        <div class="mt-4 flex flex-col sm:flex-row sm:items-center sm:justify-center gap-x-6 gap-y-1">
          <p class="text-sm font-medium text-gray-700" data-test="patience-counter">
            <span class="text-gray-400 mr-1">●</span>{{ patienceLabel }}
          </p>
          <p class="text-sm text-gray-500">Tiền là con số trên màn hình. Mất tiền là thật.</p>
        </div>
      </div>

      <div class="border-t border-gray-100 px-6 py-3 bg-gray-50">
        <div *ngIf="pickerVisible; else chosen"
             class="flex flex-wrap items-center justify-center gap-2">
          <span class="text-sm text-gray-600 mr-1">Giờ anh đang thế nào?</span>
          <button *ngFor="let option of moodOptions"
                  type="button"
                  (click)="choose(option)"
                  [attr.data-test]="'mood-' + option"
                  class="px-3 py-1.5 rounded-lg border border-gray-300 bg-white text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors">
            {{ label(option) }}
          </button>
        </div>

        <ng-template #chosen>
          <div class="flex items-center justify-center gap-2 text-sm">
            <span class="text-gray-600">Đang:</span>
            <span class="font-medium"
                  [class.text-emerald-600]="!isEmotional"
                  [class.text-amber-600]="isEmotional"
                  data-test="mood-current">{{ label(mood!) }}</span>
            <button type="button" (click)="reopen()" data-test="mood-change"
                    class="text-gray-400 hover:text-gray-600 underline underline-offset-2">đổi</button>
          </div>
        </ng-template>
      </div>
    </div>
  `,
  styles: []
})
export class PatienceHeroComponent implements OnInit {
  /** Trang chủ nghe để quyết định có phủ mờ Hàng đợi quyết định không. */
  @Output() moodChange = new EventEmitter<TodayMoodDto>();

  readonly moodOptions: MoodState[] = ['Calm', 'Fomo', 'Fear', 'Revenge'];

  mood: MoodState | null = null;
  overrode = false;
  daysSince: number | null = null;

  /**
   * Mở bảng chọn KHÔNG xoá tâm trạng đang có. Nếu "đổi" mà gán mood về null thì trời sáng
   * lại và lớp phủ biến mất — bấm "đổi" sẽ thành đường thoát khỏi chính luật dừng này.
   */
  private pickerOpen = false;

  constructor(private moodService: MoodService, private tradeService: TradeService) {}

  ngOnInit(): void {
    this.tradeService.getLastActivity()
      .pipe(catchError(() => of({ lastTradeDate: null, daysSince: null })))
      .subscribe(activity => { this.daysSince = activity.daysSince; });

    this.moodService.getToday()
      .pipe(catchError(() => of({ mood: null, overrode: false } as TodayMoodDto)))
      .subscribe(today => {
        this.mood = today.mood;
        this.overrode = today.overrode;
        this.moodChange.emit(today);
      });
  }

  /** 0 = vừa động tay, 1 = phẳng như gương. Chưa có lệnh nào thì coi như lặng. */
  get calm(): number {
    if (this.daysSince === null) return 1;
    return Math.min(this.daysSince, CALM_CEILING_DAYS) / CALM_CEILING_DAYS;
  }

  get isEmotional(): boolean {
    return this.mood !== null && this.mood !== 'Calm';
  }

  get quote(): Quote {
    return pickQuote((this.mood ?? 'Calm') as MoodKey, this.seed);
  }

  get patienceLabel(): string {
    if (this.daysSince === null) return 'Chưa có lệnh nào';
    if (this.daysSince === 0) return 'Hôm nay vừa đặt lệnh';
    return 'Đã ' + this.daysSince + ' ngày chưa đặt lệnh';
  }

  label(mood: MoodState): string {
    return MOOD_LABELS[mood as MoodKey];
  }

  get pickerVisible(): boolean {
    return this.mood === null || this.pickerOpen;
  }

  choose(mood: MoodState): void {
    const previous = this.mood;
    this.mood = mood;
    this.pickerOpen = false;
    // Đổi tâm trạng thì dấu bỏ qua lớp phủ bị xoá ở server — gương ở client theo đúng luật đó.
    if (previous !== mood) this.overrode = false;
    this.moodChange.emit({ mood, overrode: this.overrode });

    this.moodService.setMood(mood).pipe(catchError(() => of(void 0))).subscribe();
  }

  reopen(): void {
    this.pickerOpen = true;
  }

  /** Khoá ngày địa phương, chỉ để cùng một ngày ra cùng một câu. Ngày thật do server chấm. */
  private get seed(): string {
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return now.getFullYear() + '-' + pad(now.getMonth() + 1) + '-' + pad(now.getDate());
  }
}
