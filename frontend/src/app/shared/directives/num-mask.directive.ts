import { Directive, ElementRef, HostListener, Input, OnDestroy, OnInit, Optional } from '@angular/core';
import { NgModel } from '@angular/forms';
import { Subscription } from 'rxjs';

/**
 * NumMaskDirective — format number inputs with vi-VN thousand separators.
 *
 * Usage:
 *   <input type="text" inputmode="numeric" appNumMask [(ngModel)]="price">
 *   <input type="text" inputmode="numeric" appNumMask [decimals]="1" [(ngModel)]="riskPercent">
 *
 * - Formats on blur:  1000000  →  1.000.000
 * - Strips on focus:  1.000.000 →  1000000  (for easy editing)
 * - Supports optional decimal places (default 0)
 */
@Directive({
  selector: 'input[appNumMask]',
  standalone: true,
})
export class NumMaskDirective implements OnInit, OnDestroy {
  @Input() decimals = 0;

  private isEditing = false;
  private sub?: Subscription;

  constructor(
    private host: ElementRef<HTMLInputElement>,
    @Optional() private ngModel: NgModel,
  ) {}

  ngOnInit(): void {
    // Subscribe to programmatic updates (auto-fill, form reset, etc.)
    this.sub = this.ngModel?.valueChanges?.subscribe(val => {
      if (!this.isEditing) {
        const num = typeof val === 'number' ? val : parseFloat(String(val ?? '')) || 0;
        this.host.nativeElement.value = num ? this.format(num) : '';
      }
    });

    // Format initial value after first render cycle
    setTimeout(() => {
      const num = this.ngModel?.value as number | null;
      if (!this.isEditing && num) {
        this.host.nativeElement.value = this.format(num);
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  @HostListener('focus')
  onFocus(): void {
    this.isEditing = true;
    const el = this.host.nativeElement;
    const raw = this.parseFormatted(el.value);
    el.value = raw ? this.toEditStr(raw) : '';
    setTimeout(() => el.select());
  }

  @HostListener('blur')
  onBlur(): void {
    this.isEditing = false;
    const el = this.host.nativeElement;
    const raw = this.parseRaw(el.value);
    el.value = raw ? this.format(raw) : '';
    if (raw !== this.ngModel?.value) {
      this.ngModel?.update.emit(raw || null);
    }
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  /** Parse a vi-VN formatted string: dots = thousands, comma = decimal */
  private parseFormatted(val: string): number {
    return parseFloat(val.replace(/\./g, '').replace(',', '.')) || 0;
  }

  /** Parse raw user-typed string (not yet formatted) */
  private parseRaw(val: string): number {
    if (!val?.trim()) return 0;
    if (this.decimals === 0) {
      return parseInt(val.replace(/\D/g, ''), 10) || 0;
    }
    // Decimal mode: accept comma or dot as decimal separator
    if (val.includes(',')) {
      return parseFloat(val.replace(/\./g, '').replace(',', '.')) || 0;
    }
    return parseFloat(val.replace(/[^\d.]/g, '')) || 0;
  }

  private toEditStr(value: number): string {
    return this.decimals > 0 ? value.toFixed(this.decimals) : String(Math.round(value));
  }

  private format(value: number): string {
    return new Intl.NumberFormat('vi-VN', {
      maximumFractionDigits: this.decimals,
      minimumFractionDigits: 0,
    }).format(value);
  }
}
