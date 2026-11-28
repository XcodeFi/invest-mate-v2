import { contrastRatio } from '../../theme/contrast';
import { ChartColors, chartColors } from './indicator-chart.theme';

/** Mọi màu CHỮ của biểu đồ (nhãn đường ngang, nhãn marker, chữ trên khung giá) phải đạt AA (>= 4,5:1) so với nền biểu đồ của CHÍNH chế độ đó. */
describe('indicator-chart.theme: màu chữ đạt tương phản AA ở cả hai giao diện', () => {
  const TEXT_TOKENS: (keyof ChartColors)[] = ['text', 'trapBull', 'trapBear', 'lineLoss', 'lineProfit', 'lineEntry', 'lineFib', 'lineSwing'];

  for (const theme of ['light', 'dark'] as const) {
    for (const token of TEXT_TOKENS) {
      it(`${token} on the ${theme} chart background is >= 4.5:1`, () => {
        const colors = chartColors(theme);

        expect(contrastRatio(colors[token], colors.background)).toBeGreaterThanOrEqual(4.5);
      });
    }
  }

  it('the light and dark palettes define every token (none left undefined by a typo)', () => {
    for (const theme of ['light', 'dark'] as const) {
      for (const token of TEXT_TOKENS) expect(chartColors(theme)[token]).withContext(`${theme}/${token}`).toMatch(/^#[0-9a-f]{6}$/i);
    }
  });
});
