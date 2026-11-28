import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReviewOpenWarningComponent } from './review-open-warning.component';

describe('ReviewOpenWarningComponent', () => {
  let fixture: ComponentFixture<ReviewOpenWarningComponent>;

  const text = (): string => fixture.nativeElement.textContent.replace(/\s+/g, ' ');
  const q = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);

  function render(inputs: Record<string, unknown>) {
    TestBed.configureTestingModule({ imports: [ReviewOpenWarningComponent] });
    fixture = TestBed.createComponent(ReviewOpenWarningComponent);
    Object.entries(inputs).forEach(([k, v]) => fixture.componentRef.setInput(k, v));
    fixture.detectChanges();
  }

  it('warns with the open quantity when shares remain', () => {
    render({ openQuantity: 500 });

    expect(q('review-open-warning')).not.toBeNull();
    expect(text()).toContain('Còn 500 CP chưa bán — kết quả chỉ tính phần đã chốt');
  });

  it('shows nothing for a closed review (0) or an old review (null)', () => {
    render({ openQuantity: 0 });
    expect(q('review-open-warning')).toBeNull();
    fixture.componentRef.setInput('openQuantity', null);
    fixture.detectChanges();
    expect(q('review-open-warning')).toBeNull();
  });

  it('shows the cash dividend included in P&L only when there is one', () => {
    render({ openQuantity: 0, dividendIncome: 950000 });
    expect(q('review-dividend-income')).not.toBeNull();
    expect(text()).toContain('950,000');

    fixture.componentRef.setInput('dividendIncome', 0);
    fixture.detectChanges();
    expect(q('review-dividend-income')).toBeNull();
  });
});
