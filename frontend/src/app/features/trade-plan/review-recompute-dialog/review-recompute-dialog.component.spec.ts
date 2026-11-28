import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReviewRecomputeDialogComponent } from './review-recompute-dialog.component';

describe('ReviewRecomputeDialogComponent', () => {
  let fixture: ComponentFixture<ReviewRecomputeDialogComponent>;
  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);
  const text = (id: string): string => (q(id)?.textContent ?? '').replace(/\s+/g, ' ');

  function render(inputs: Record<string, unknown>) {
    TestBed.configureTestingModule({ imports: [ReviewRecomputeDialogComponent] });
    fixture = TestBed.createComponent(ReviewRecomputeDialogComponent);
    Object.entries(inputs).forEach(([k, v]) => fixture.componentRef.setInput(k, v));
    fixture.detectChanges();
  }

  const before = { pnLAmount: 143_900_000, pnLPercent: 99, realizedR: 20 };
  const after = { pnLAmount: 0, pnLPercent: 0, realizedR: null };

  it('shows the stored and recomputed numbers side by side, R null as n/a not 0R', () => {
    render({ before, after });

    expect(text('recompute-r')).toContain('+20.00R');
    expect(text('recompute-r')).toContain('n/a');
    // sau khi tính lại R là null: chỉ được hiện n/a, không có "0.00R" đứng riêng (khác "+20.00R")
    const afterCell = (q('recompute-r')!.querySelectorAll('td')[2].textContent ?? '').trim();
    expect(afterCell).toBe('n/a');
    expect(text('recompute-pct')).toContain('99.00%');
  });

  it('puts the primary button on the right and the overlay above the header (z-[60])', () => {
    render({ before, after });

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLElement[];
    expect(buttons.map(b => b.textContent!.trim())).toEqual(['Hủy', 'Tính lại']);
    expect(q('recompute-dialog')!.className).toContain('z-[60]');
  });

  it('emits confirm and cancel', () => {
    render({ before, after });
    const confirm = jasmine.createSpy('confirm');
    const cancel = jasmine.createSpy('cancel');
    fixture.componentInstance.confirm.subscribe(confirm);
    fixture.componentInstance.cancel.subscribe(cancel);

    q('recompute-confirm')!.click();
    q('recompute-cancel')!.click();

    expect(confirm).toHaveBeenCalledTimes(1);
    expect(cancel).toHaveBeenCalledTimes(1);
  });

  it('disables the confirm button while busy', () => {
    render({ before, after, busy: true });

    expect((q('recompute-confirm') as HTMLButtonElement).disabled).toBeTrue();
  });
});
