import { TestBed } from '@angular/core/testing';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent — tiêu đề khối đầu tư', () => {
  const make = () => TestBed.runInInjectionContext(() =>
    new DashboardComponent(...(Array.from({ length: 13 }, () => ({})) as ConstructorParameters<typeof DashboardComponent>)));

  it('the investment hero is titled "Tổng quan đầu tư" so "Tổng tài sản" keeps a single meaning', () => {
    const title = make().investmentHeroTitle;

    expect(title).toBe('Tổng quan đầu tư');
    expect(title).not.toContain('Tổng tài sản');
  });
});
