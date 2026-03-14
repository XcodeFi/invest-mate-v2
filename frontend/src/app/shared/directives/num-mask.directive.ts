import { Directive, ElementRef, forwardRef, HostListener, Input, Renderer2 } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * NumMaskDirective — ControlValueAccessor that formats number inputs
 * with vi-VN thousand separators.
 *
 * Model value is ALWAYS a pure number (or null).
 * Display value shows formatted text (e.g. 1.000.000 or 12,5).
 *
 * Usage:
 *   <input type="text" inputmode="numeric" appNumMask [(ngModel)]="price">
 *   <input type="text" inputmode="numeric" appNumMask [decimals]="1" [(ngModel)]="riskPercent">
 */
@Directive({
  selector: 'input[appNumMask]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NumMaskDirective),
      multi: true,
    },
  ],
})
export class NumMaskDirective implements ControlValueAccessor {
  /** Number of decimal places allowed (0 = integer only, 1+ = decimal) */
  @Input() decimals = 0;

  /** When true, display empty string instead of "0" when value is 0 */
  @Input() emptyWhenZero = false;

  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};
  private isEditing = false;

  constructor(private el: ElementRef<HTMLInputElement>, private renderer: Renderer2) {}

  // ── CVA interface ────────────────────────────────────────────────────────

  /** Called when model value changes programmatically (patchValue, setValue, ngModel binding) */
  writeValue(value: any): void {
    const num = this.toNumber(value);
    if (!this.isEditing) {
      this.setDisplay(num);
    }
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.renderer.setProperty(this.el.nativeElement, 'disabled', isDisabled);
  }

  // ── Event handlers ───────────────────────────────────────────────────────

  /** Block non-numeric keys (allow decimals when decimals > 0) */
  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    const allowed = ['Backspace', 'Tab', 'End', 'Home', 'ArrowLeft', 'ArrowRight', 'Delete'];
    if (allowed.includes(event.key)) return;
    if (event.ctrlKey || event.metaKey) return; // Allow Ctrl+A, Ctrl+C, etc.

    // Allow digits
    if (/^[0-9]$/.test(event.key)) return;

    // Allow decimal separator (dot or comma) if decimals > 0
    if (this.decimals > 0 && (event.key === '.' || event.key === ',')) {
      const val = this.el.nativeElement.value;
      // Only allow one decimal separator
      if (val.includes('.') || val.includes(',')) {
        event.preventDefault();
      }
      return;
    }

    // Allow minus sign at start
    if (event.key === '-' && this.el.nativeElement.selectionStart === 0) {
      if (!this.el.nativeElement.value.includes('-')) return;
    }

    event.preventDefault();
  }

  /** On each keystroke: parse raw input → emit number → reformat display */
  @HostListener('input')
  onInput(): void {
    const el = this.el.nativeElement;
    const cursorPos = el.selectionStart ?? 0;
    const oldLength = el.value.length;

    const num = this.parseRaw(el.value);
    this.onChange(num);

    // Reformat display while keeping cursor position reasonable
    const formatted = this.formatForEdit(num);
    this.renderer.setProperty(el, 'value', formatted);

    // Adjust cursor position after reformatting
    const newLength = formatted.length;
    const newPos = Math.max(0, cursorPos + (newLength - oldLength));
    el.setSelectionRange(newPos, newPos);
  }

  /** On focus: show raw number for easy editing */
  @HostListener('focus')
  onFocus(): void {
    this.isEditing = true;
    const el = this.el.nativeElement;
    const num = this.parseFormatted(el.value);
    el.value = this.formatForEdit(num);
    setTimeout(() => el.select());
  }

  /** On blur: reformat with full locale formatting */
  @HostListener('blur')
  onBlur(): void {
    this.isEditing = false;
    const el = this.el.nativeElement;
    const num = this.parseRaw(el.value);
    this.setDisplay(num);
    this.onTouched();
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  /** Convert any input to number or null */
  private toNumber(value: any): number | null {
    if (value == null || value === '') return null;
    const n = typeof value === 'number' ? value : parseFloat(String(value));
    return isNaN(n) ? null : n;
  }

  /** Parse user's raw typing (digits, possibly one decimal separator) */
  private parseRaw(val: string): number | null {
    if (!val?.trim()) return null;

    if (this.decimals === 0) {
      // Integer mode: strip all non-digits (keep minus)
      const neg = val.startsWith('-');
      const digits = val.replace(/[^0-9]/g, '');
      if (!digits) return null;
      const n = parseInt(digits, 10);
      return neg ? -n : n;
    }

    // Decimal mode: normalize separators
    // User might type: 12.5 or 12,5 or 1.234,5 (vi-VN formatted)
    let normalized = val;
    // If contains both dot and comma, dot is thousands separator
    if (val.includes('.') && val.includes(',')) {
      normalized = val.replace(/\./g, '').replace(',', '.');
    } else if (val.includes(',')) {
      // Single comma = decimal separator
      normalized = val.replace(',', '.');
    }
    // Strip remaining non-numeric chars except dot and minus
    normalized = normalized.replace(/[^0-9.\-]/g, '');
    const n = parseFloat(normalized);
    return isNaN(n) ? null : n;
  }

  /** Parse a vi-VN formatted display string back to number */
  private parseFormatted(val: string): number | null {
    if (!val?.trim()) return null;
    // vi-VN: 1.000.000 (dots = thousands) or 1.234,5 (comma = decimal)
    const normalized = val.replace(/\./g, '').replace(',', '.');
    const n = parseFloat(normalized);
    return isNaN(n) ? null : n;
  }

  /** Format for display during editing (with thousand separators) */
  private formatForEdit(value: number | null): string {
    if (value == null) return '';
    if (this.decimals === 0) {
      // Integer: format with thousand separators using dots
      return new Intl.NumberFormat('vi-VN', {
        maximumFractionDigits: 0,
        minimumFractionDigits: 0,
      }).format(value);
    }
    // Decimal: show as-is for easy editing
    return String(value);
  }

  /** Set the display value (formatted for viewing) */
  private setDisplay(value: number | null): void {
    const isEmpty = value == null || (this.emptyWhenZero && value === 0);
    const formatted = !isEmpty
      ? new Intl.NumberFormat('vi-VN', {
          maximumFractionDigits: this.decimals,
          minimumFractionDigits: 0,
        }).format(value!)
      : '';
    this.renderer.setProperty(this.el.nativeElement, 'value', formatted);
  }
}
