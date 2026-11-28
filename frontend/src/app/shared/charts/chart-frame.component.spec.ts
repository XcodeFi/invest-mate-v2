import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChartFrameComponent } from './chart-frame.component';

@Component({
  standalone: true,
  imports: [ChartFrameComponent],
  template: `
    <app-chart-frame title="Doanh thu" [hasData]="hasData">
      <svg data-testid="the-chart"></svg>
      <table table data-testid="the-table"><tr><td>12</td></tr></table>
    </app-chart-frame>`
})
class HostComponent { hasData = true; }

describe('ChartFrameComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  const q = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const toggle = () => q('chart-table-toggle') as HTMLButtonElement;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('shows the chart first and keeps the table in the DOM but hidden', () => {
    expect(q('chart-title')!.textContent).toBe('Doanh thu');
    expect(q('chart-body')!.hidden).toBeFalse();
    expect(q('chart-table')!.hidden).toBeTrue();
    expect(q('the-table')).not.toBeNull();
  });

  it('table_toggle_shows_same_numbers: the toggle swaps chart and table and relabels itself', () => {
    expect(toggle().textContent).toBe('Xem bảng');
    expect(toggle().getAttribute('aria-pressed')).toBe('false');

    toggle().click();
    fixture.detectChanges();

    expect(q('chart-body')!.hidden).toBeTrue();
    expect(q('chart-table')!.hidden).toBeFalse();
    expect(q('chart-table')!.textContent).toContain('12');
    expect(toggle().textContent).toBe('Xem biểu đồ');
    expect(toggle().getAttribute('aria-pressed')).toBe('true');

    toggle().click();
    fixture.detectChanges();
    expect(q('chart-body')!.hidden).toBeFalse();
  });

  it('missing_section_shows_chua_co_du_lieu: no toggle, no chart, no table, just the sentence', () => {
    fixture.componentInstance.hasData = false;
    fixture.detectChanges();

    expect(q('chart-empty')!.textContent).toBe('Chưa có dữ liệu');
    expect(q('chart-table-toggle')).toBeNull();
    expect(q('chart-body')).toBeNull();
  });

  it('the body scrolls on its own so the page never scrolls sideways', () => {
    const styles = getComputedStyle(q('chart-body')!);

    expect(styles.overflowX).toBe('auto');
  });
});
