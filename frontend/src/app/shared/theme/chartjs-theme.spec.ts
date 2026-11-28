import { applyChartJsDefaults, chartJsColors } from './chartjs-theme';
import { contrastRatio } from './contrast';

describe('chartjs-theme', () => {
  it('dark: light text and a faint light grid; the slice border matches the card', () => {
    const c = chartJsColors('dark');

    expect(c.text).toBe('#cbd5e1');
    expect(c.grid).toContain('148, 163, 184');
    expect(c.slice).toBe('#111827');
    expect(contrastRatio(c.text, '#111827')).toBeGreaterThanOrEqual(4.5);
  });

  it('light keeps the previous Chart.js look exactly', () => {
    const c = chartJsColors('light');

    expect(c.text).toBe('#666666');
    expect(c.grid).toBe('rgba(0, 0, 0, 0.05)');
    expect(c.slice).toBe('#f9fafb');
  });

  it('applyChartJsDefaults writes the global defaults and switches back', () => {
    const fake = { defaults: { color: 'x' as unknown, borderColor: 'y' as unknown } };

    applyChartJsDefaults(fake, 'dark');
    expect(fake.defaults.color).toBe('#cbd5e1');
    expect(fake.defaults.borderColor).toBe(chartJsColors('dark').grid);

    applyChartJsDefaults(fake, 'light');
    expect(fake.defaults.color).toBe('#666666');
    expect(fake.defaults.borderColor).toBe(chartJsColors('light').grid);
  });
});
