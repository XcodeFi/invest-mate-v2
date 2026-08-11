import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface WaveLine {
  d: string;
  width: number;
  opacity: number;
}

/** Trộn hai màu hex theo tỷ lệ t (0 = a, 1 = b). */
function mix(a: string, b: string, t: number): string {
  const parse = (hex: string) => [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16));
  const [ar, ag, ab] = parse(a);
  const [br, bg, bb] = parse(b);
  const c = (x: number, y: number) => Math.round(x + (y - x) * t);
  return 'rgb(' + c(ar, br) + ',' + c(ag, bg) + ',' + c(ab, bb) + ')';
}

/**
 * Cảnh người ngồi câu. Thuần trình bày — không biết gì về giao dịch, chỉ nhận hai đầu vào:
 *
 * - `calm` (0..1): mặt hồ và bầu trời. 0 = vừa động tay (sóng gấp, trời xám),
 *   1 = đã lâu không động tay (phẳng như gương, hoàng hôn ấm).
 * - `dim`: tối lớp trời đi một bậc khi người dùng tự chấm là đang có cảm xúc.
 *   Cố ý KHÔNG đụng vào sóng — sóng đang kể một sự thật khác (số ngày chưa đặt lệnh)
 *   và tâm trạng không được viết đè lên sự thật đó.
 *
 * Hoạt hoạ chạy bằng CSS, không có vòng lặp JavaScript: trang này đã có Chart.js
 * và hơn mười khối số liệu, không thêm việc cho luồng chính.
 */
@Component({
  selector: 'app-fishing-scene',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="scene" [class.reduced-motion]="reducedMotion" [style]="cssVars">
      <!-- viewBox theo đúng tỷ lệ khung thật (~1200×160). Dùng 400×140 với "slice" thì trên
           màn rộng nó phóng theo chiều ngang rồi cắt cụt chiều dọc — mất đầu người câu.
           Neo xMin để trên màn hẹp phần bị cắt là mặt nước trống bên phải, không phải nhân vật. -->
      <svg viewBox="0 0 1200 160" preserveAspectRatio="xMinYMid slice"
           class="w-full h-32 sm:h-40" role="img"
           [attr.aria-label]="ariaLabel">
        <defs>
          <linearGradient id="patience-sky" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" [attr.stop-color]="skyTop"/>
            <stop offset="100%" [attr.stop-color]="skyBottom"/>
          </linearGradient>
        </defs>

        <rect x="0" y="0" width="1200" height="100" fill="url(#patience-sky)"/>
        <circle cx="1010" cy="40" r="18" [attr.fill]="sunColor" [attr.opacity]="0.5 + calm * 0.5"/>

        <rect x="0" y="96" width="1200" height="64" [attr.fill]="waterColor"/>

        <g class="waves" [attr.stroke]="waveColor" fill="none" stroke-linecap="round">
          <path *ngFor="let w of waves; let i = index"
                class="wave" [class]="'wave wave-' + (i + 1)"
                [attr.d]="w.d" [attr.stroke-width]="w.width" [attr.opacity]="w.opacity"/>
        </g>

        <ellipse cx="150" cy="99" rx="88" ry="9" [attr.fill]="bankColor"/>

        <g class="angler" [attr.stroke]="figureColor" stroke-width="3.2"
           stroke-linecap="round" fill="none">
          <circle cx="140" cy="48" r="8" [attr.fill]="figureColor" stroke="none"/>
          <path d="M140 56 L140 80"/>
          <path d="M140 63 L166 55"/>
          <path d="M140 80 L130 97"/>
          <path d="M140 80 L152 97"/>
        </g>

        <line class="rod" x1="166" y1="55" x2="310" y2="22"
              [attr.stroke]="figureColor" stroke-width="2.4" stroke-linecap="round"/>
        <path class="fishing-line" d="M310 22 Q 430 60 545 104"
              [attr.stroke]="figureColor" stroke-width="1.2" fill="none" opacity="0.55"/>
        <circle class="float" cx="545" cy="104" r="5" [attr.fill]="floatColor"/>
      </svg>
    </div>
  `,
  styles: [`
    .scene {
      --amp: 3px;
      --dur: 5s;
      --drift: 14s;
      display: block;
      overflow: hidden;
      border-radius: 0.75rem 0.75rem 0 0;
    }
    .wave { animation: drift var(--drift) linear infinite; }
    .wave-2 { animation-duration: calc(var(--drift) * 1.6); }
    .wave-3 { animation-duration: calc(var(--drift) * 2.3); }
    .float { animation: bob var(--dur) ease-in-out infinite; }
    .angler { animation: sway calc(var(--dur) * 2.4) ease-in-out infinite; }

    /* Trượt đúng một chu kỳ sóng (200 đơn vị viewBox) nên vòng lặp không lộ mối nối. */
    @keyframes drift {
      from { transform: translateX(0); }
      to   { transform: translateX(200px); }
    }
    @keyframes bob {
      0%, 100% { transform: translateY(calc(var(--amp) * -1)); }
      50%      { transform: translateY(var(--amp)); }
    }
    @keyframes sway {
      0%, 100% { transform: translateX(0); }
      50%      { transform: translateX(0.6px); }
    }

    .reduced-motion .wave,
    .reduced-motion .float,
    .reduced-motion .angler { animation: none; }

    @media (prefers-reduced-motion: reduce) {
      .wave, .float, .angler { animation: none; }
    }
  `]
})
export class FishingSceneComponent {
  private _calm = 0;
  private _cssVars: Record<string, string> = FishingSceneComponent.buildVars(0);
  private _waves: WaveLine[] = FishingSceneComponent.buildWaves(0);

  /** 0 = vừa động tay, 1 = mặt hồ phẳng. Ngoài khoảng thì bị kẹp về biên. */
  @Input()
  set calm(value: number) {
    const n = Number(value);
    this._calm = Number.isFinite(n) ? Math.min(1, Math.max(0, n)) : 0;
    this._cssVars = FishingSceneComponent.buildVars(this._calm);
    this._waves = FishingSceneComponent.buildWaves(this._calm);
  }
  get calm(): number {
    return this._calm;
  }

  /** Ba đường sóng, biên độ dựng theo `calm` — cùng lý do memo hoá như cssVars. */
  get waves(): WaveLine[] {
    return this._waves;
  }

  private static buildVars(calm: number): Record<string, string> {
    return {
      '--amp': (1 + (1 - calm) * 5).toFixed(2) + 'px',
      '--dur': (2.2 + calm * 4.5).toFixed(2) + 's',
      '--drift': (6 + calm * 16).toFixed(2) + 's',
    };
  }

  /**
   * Biên độ phải nằm trong chính đường vẽ, không thể đẩy sang CSS: hình dạng sóng là dữ liệu
   * path. Trước đây để cứng nên hồ "phẳng như gương" vẫn gợn y hệt lúc vừa đặt lệnh.
   */
  private static buildWaves(calm: number): WaveLine[] {
    const scale = 1 - calm * 0.88;   // calm = 1 còn ~12% biên độ, đủ thấy mặt nước chứ không chết cứng
    return [
      { d: FishingSceneComponent.wavePath(110, 12 * scale), width: 2, opacity: 0.8 },
      { d: FishingSceneComponent.wavePath(128, 10 * scale), width: 1.6, opacity: 0.5 },
      { d: FishingSceneComponent.wavePath(146, 8 * scale), width: 1.3, opacity: 0.3 },
    ];
  }

  /** Chu kỳ 200 đơn vị, khớp với quãng trượt 200px của keyframe nên vòng lặp không lộ mối nối. */
  private static wavePath(y: number, amp: number): string {
    let d = 'M-200 ' + y + ' q 50 ' + (-amp).toFixed(2) + ' 100 0';
    for (let i = 0; i < 15; i++) d += ' t 100 0';
    return d;
  }

  /** Tối trời đi một bậc khi tâm trạng khác Bình tĩnh. Không đụng vào sóng. */
  @Input() dim = false;

  readonly reducedMotion: boolean =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  private get shade(): number {
    return this.dim ? 0.72 : 1;
  }

  get skyTop(): string {
    return this.darken(mix('#64748b', '#f97316', this.calm));
  }
  get skyBottom(): string {
    return this.darken(mix('#94a3b8', '#fcd34d', this.calm));
  }
  get sunColor(): string {
    return this.darken(mix('#cbd5e1', '#fef3c7', this.calm));
  }
  get waterColor(): string {
    return this.darken(mix('#475569', '#0e7490', this.calm));
  }
  get waveColor(): string {
    return this.darken(mix('#cbd5e1', '#67e8f9', this.calm));
  }
  get bankColor(): string {
    return this.darken(mix('#334155', '#155e75', this.calm));
  }
  get figureColor(): string {
    return this.darken('#0f172a');
  }
  get floatColor(): string {
    return this.darken(mix('#f8fafc', '#fb7185', this.calm));
  }

  /**
   * Sóng thấp và chậm dần khi mặt hồ lặng lại.
   * Dựng sẵn khi `calm` đổi chứ không dựng trong getter: trang chủ này còn chạy Chart.js
   * và hơn mười khối số liệu, cấp phát object mới mỗi vòng dò thay đổi là phí không cần thiết.
   */
  get cssVars(): Record<string, string> {
    return this._cssVars;
  }

  get ariaLabel(): string {
    if (this.calm >= 0.9) return 'Mặt hồ phẳng lặng lúc hoàng hôn, người ngồi câu bất động';
    if (this.calm >= 0.5) return 'Mặt hồ gợn lăn tăn, người ngồi câu';
    return 'Mặt nước còn động, người ngồi câu';
  }

  private darken(color: string): string {
    if (this.shade === 1) return color;
    const nums = color.startsWith('#')
      ? [1, 3, 5].map(i => parseInt(color.slice(i, i + 2), 16))
      : color.replace(/[^\d,]/g, '').split(',').map(Number);
    return 'rgb(' + nums.map(n => Math.round(n * this.shade)).join(',') + ')';
  }
}
