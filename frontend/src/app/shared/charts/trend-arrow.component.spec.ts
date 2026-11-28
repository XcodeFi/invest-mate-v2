import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TrendArrowComponent } from './trend-arrow.component';

@Component({
  standalone: true,
  imports: [TrendArrowComponent],
  template: `<app-trend-arrow [direction]="direction" [pct]="pct" [note]="note" [neutral]="neutral" [label]="label"></app-trend-arrow>`
})
class HostComponent {
  direction: 'up' | 'down' | 'flat' | null = 'up';
  pct: number | null = 12.34;
  note: string | null = null;
  neutral = false;
  label = '';
}

describe('TrendArrowComponent: mũi tên xu hướng (hướng theo SỐ, màu chỉ là mã hoá phụ)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const arrow = () => fixture.nativeElement.querySelector('[data-testid="trend-arrow"]') as HTMLElement | null;
  const text = () => (arrow()?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const show = (over: Partial<HostComponent>) => { Object.assign(host, over); fixture.detectChanges(); };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('up: ▲ with sign and percent', () => {
    expect(text()).toBe('▲ +12,3%');
    expect(arrow()!.getAttribute('data-direction')).toBe('up');
  });

  it('down: ▼ with a real minus', () => {
    show({ direction: 'down', pct: -8.1 });

    expect(text()).toBe('▼ −8,1%');
  });

  it('flat: ▬ with the small percent', () => {
    show({ direction: 'flat', pct: 0.3 });

    expect(text()).toBe('▬ +0,3%');
  });

  it('no_arrow_when_no_comparable_period: nothing is drawn', () => {
    show({ direction: null, pct: null });

    expect(arrow()).toBeNull();
  });

  it('a direction with no percentage shows the note instead (loss to profit)', () => {
    show({ direction: 'up', pct: null, note: 'từ lỗ sang lãi' });

    expect(text()).toBe('▲ từ lỗ sang lãi');
  });

  it('a direction with neither percentage nor note still says which way', () => {
    show({ direction: 'down', pct: null, note: null });

    expect(text()).toBe('▼ giảm');
  });

  it('meaning never depends on colour: sign, percent and an accessible name are always present', () => {
    show({ direction: 'down', pct: -8.1, label: 'so với cùng kỳ' });

    expect(text()).toContain('−8,1%');
    expect(text()).toContain('so với cùng kỳ');
    expect(arrow()!.getAttribute('aria-label')).toBe('giảm −8,1% so với cùng kỳ');
  });

  it('colour is a secondary channel: green up, red down, grey flat', () => {
    expect(arrow()!.className).toContain('viz-up');
    show({ direction: 'down', pct: -3 });
    expect(arrow()!.className).toContain('viz-down');
    show({ direction: 'flat', pct: 0.2 });
    expect(arrow()!.className).toContain('viz-flat');
  });

  it('a metric that is bad when up keeps the arrow direction but loses the colour (neutral grey)', () => {
    show({ direction: 'up', pct: 15, neutral: true });

    expect(text()).toBe('▲ +15,0%');
    expect(arrow()!.className).toContain('viz-flat');
    expect(arrow()!.className).not.toContain('viz-up');
  });

  it('the arrow follows the number even for a note-only direction', () => {
    show({ direction: 'down', pct: null, note: 'từ lãi sang lỗ' });

    expect(text()).toBe('▼ từ lãi sang lỗ');
  });
});
