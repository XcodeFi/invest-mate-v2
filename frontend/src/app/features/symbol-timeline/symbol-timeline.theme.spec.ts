import { TestBed } from '@angular/core/testing';
import { SymbolTimelineComponent } from './symbol-timeline.component';
import { ThemeService } from '../../core/services/theme.service';
import { contrastRatio } from '../../shared/theme/contrast';

describe('SymbolTimelineComponent — màu cảm xúc theo chế độ', () => {
  const make = () => TestBed.runInInjectionContext(() => new SymbolTimelineComponent({} as any, {} as any, {} as any, {} as any, {} as any, {} as any));
  const hadDark = document.documentElement.classList.contains('dark');
  afterEach(() => document.documentElement.classList.toggle('dark', hadDark));

  it('light mode keeps the palette as is (FOMO stays near-black)', () => {
    TestBed.inject(ThemeService).set('light', false);

    expect(make().getEmotionColor('FOMO')).toBe('#1f2937');
    expect(make().getEmotionColor('Tự tin')).toBe('#22c55e');
  });

  it('dark mode lightens a colour too dark for the card (FOMO) to at least 4.5:1, others keep their identity', () => {
    TestBed.inject(ThemeService).set('dark', false);
    const sut = make();

    expect(contrastRatio(sut.getEmotionColor('FOMO'), '#111827')).toBeGreaterThanOrEqual(4.5);
    expect(sut.getEmotionColor('Tự tin')).toBe('#22c55e');
    expect(sut.getEmotionColor('Sợ hãi')).toBe('#ef4444');
  });

  it('unknown emotions fall back to a readable grey in dark mode too', () => {
    TestBed.inject(ThemeService).set('dark', false);

    expect(contrastRatio(make().getEmotionColor('Lạ'), '#111827')).toBeGreaterThanOrEqual(4.5);
  });
});
