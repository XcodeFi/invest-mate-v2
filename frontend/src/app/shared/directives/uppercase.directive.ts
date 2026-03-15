import { Directive, ElementRef, forwardRef, HostListener } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * UppercaseDirective — ControlValueAccessor that forces input value to UPPERCASE.
 * Both the display and the model value are always uppercase + trimmed.
 *
 * Usage:
 *   <input type="text" appUppercase [(ngModel)]="symbol">
 */
@Directive({
  selector: 'input[appUppercase]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => UppercaseDirective),
      multi: true,
    },
  ],
})
export class UppercaseDirective implements ControlValueAccessor {
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private el: ElementRef<HTMLInputElement>) {}

  @HostListener('input')
  onInput(): void {
    const raw = this.el.nativeElement.value;
    const upper = raw.toUpperCase();
    if (raw !== upper) {
      const start = this.el.nativeElement.selectionStart;
      this.el.nativeElement.value = upper;
      this.el.nativeElement.setSelectionRange(start, start);
    }
    this.onChange(upper.trim());
  }

  @HostListener('blur')
  onBlur(): void {
    this.el.nativeElement.value = this.el.nativeElement.value.trim();
    this.onTouched();
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    setTimeout(() => this.onInput());
  }

  writeValue(value: string | null): void {
    this.el.nativeElement.value = (value ?? '').toUpperCase().trim();
  }

  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.el.nativeElement.disabled = isDisabled; }
}
