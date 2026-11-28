import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlanBarsComponent } from './plan-bars.component';
import { BusinessPlanTarget } from '../../core/services/market-data.service';

describe('PlanBarsComponent: kế hoạch kinh doanh', () => {
  let fixture: ComponentFixture<PlanBarsComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const qa = (id: string) => Array.from(el().querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
  const show = (targets: BusinessPlanTarget[], year: number | null = 2026, quarter: number | null = 2) => {
    fixture.componentInstance.targets = targets;
    fixture.componentInstance.year = year;
    fixture.componentInstance.quarter = quarter;
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PlanBarsComponent] });
    fixture = TestBed.createComponent(PlanBarsComponent);
  });

  it('draws a progress bar per target with actual / plan and the percent complete', () => {
    show([{ label: 'Doanh thu', planned: 1000, actual: 600, percentComplete: 60 }, { label: 'Lợi nhuận sau thuế', planned: 100, actual: 25, percentComplete: 25 }]);

    expect(qa('pb-row').map(r => r.dataset['label'])).toEqual(['Doanh thu', 'Lợi nhuận sau thuế']);
    expect(qa('pb-value')[0].textContent!.replace(/\s+/g, ' ').trim()).toBe('600 tỷ / 1.000 tỷ · 60,0%');
    expect(qa('pb-fill')[0].style.width).toBe('60%');
    expect(qa('pb-fill')[1].style.width).toBe('25%');
  });

  it('says which year and the cumulative quarter reached', () => {
    show([{ label: 'Doanh thu', planned: 1000, actual: 600, percentComplete: 60 }]);

    expect(el().textContent).toContain('Năm 2026');
    expect(el().textContent).toContain('thực hiện luỹ kế tới Q2');
  });

  it('a missing percent from the source is shown as a dash: the client never derives actual / plan itself', () => {
    show([{ label: 'A', planned: 200, actual: 50, percentComplete: null }]);

    expect(qa('pb-fill').length === 0 || qa('pb-fill')[0].style.width === '' || qa('pb-fill')[0].style.width === '0%').toBeTrue();
    expect(el().textContent).not.toContain('25,0%');
    expect(el().textContent).toContain('—');
  });

  it('more than 100% fills the bar and says "vượt kế hoạch"', () => {
    show([{ label: 'Doanh thu', planned: 100, actual: 130, percentComplete: 130 }]);

    expect(qa('pb-fill')[0].style.width).toBe('100%');
    expect(qa('pb-over').length).toBe(1);
  });

  it('a target with nothing to compare says "Chưa có dữ liệu" instead of an empty 0% bar', () => {
    show([{ label: 'Cổ tức', planned: null, actual: null, percentComplete: null }]);

    expect(qa('pb-row-empty')[0].textContent).toBe('Chưa có dữ liệu');
    expect(qa('pb-fill').length).toBe(0);
  });

  it('no targets: the section says "Chưa có dữ liệu"', () => {
    show([]);

    expect(el().querySelector('[data-testid="chart-empty"]')!.textContent).toBe('Chưa có dữ liệu');
  });

  it('table_toggle_shows_same_numbers: the table has the same values as the bars', () => {
    show([{ label: 'Doanh thu', planned: 1000, actual: 600, percentComplete: 60 }]);

    const row = qa('pb-table-row')[0].textContent!;
    expect(row).toContain('600 tỷ');
    expect(row).toContain('1.000 tỷ');
    expect(row).toContain('60,0%');
  });

  it('a label from the source is text only', () => {
    show([{ label: '<img src=x onerror=alert(1)>', planned: 100, actual: 10, percentComplete: 10 }]);

    expect(el().querySelector('img')).toBeNull();
  });
});
