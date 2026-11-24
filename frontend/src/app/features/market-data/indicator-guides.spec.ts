import { TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { INDICATOR_GUIDES, readNow } from './indicator-guides';
import { TaGroup } from './ta-group-layers';

const N = 30;
const flat = (v: number | null): (number | null)[] => Array.from({ length: N }, () => v);
const withLast = (base: number | null, last: number | null): (number | null)[] =>
  Array.from({ length: N }, (_, i) => (i === N - 1 ? last : base));
const nums = (v: number): number[] => Array.from({ length: N }, () => v);

function series(over: Partial<TechnicalSeries> = {}): TechnicalSeries {
  return {
    symbol: 'FPT', dates: Array.from({ length: N }, (_, i) => `2026-01-${String(i + 1).padStart(2, '0')}`),
    open: nums(100), high: nums(101), low: nums(99), close: nums(100), volume: nums(1000),
    ema20: flat(100), ema50: flat(100),
    bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
    rsi14: flat(50), stochK: flat(50), stochD: flat(50),
    macdLine: flat(0), macdSignal: flat(0), macdHistogram: flat(0),
    adx: flat(20), plusDi: flat(20), minusDi: flat(20),
    atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
    ...over,
  };
}

function analysis(over: Partial<TechnicalAnalysis> = {}): TechnicalAnalysis {
  return {
    symbol: 'FPT', currentPrice: 100,
    supportLevels: [95, 90], resistanceLevels: [105, 110],
    suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 110, confluenceScore: 68.5,
    ...over,
  } as TechnicalAnalysis;
}

const groups: TaGroup[] = ['tong-hop', 'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia'];

describe('INDICATOR_GUIDES', () => {
  it('has a guide for every group', () => {
    groups.forEach(g => {
      const guide = INDICATOR_GUIDES[g];
      expect(guide).withContext(g).toBeDefined();
      expect(guide.title.length).withContext(g).toBeGreaterThan(0);
      expect(guide.oneLine.length).withContext(g).toBeGreaterThan(0);
      expect(guide.items.length).withContext(g).toBeGreaterThan(0);
    });
  });

  it('every item explains what it is, where to see it, how to read it and a common pitfall', () => {
    groups.forEach(g => INDICATOR_GUIDES[g].items.forEach(item => {
      expect(item.name.length).withContext(`${g}/${item.name}`).toBeGreaterThan(0);
      expect(item.what.length).withContext(`${g}/${item.name} what`).toBeGreaterThan(10);
      expect(item.onChart.length).withContext(`${g}/${item.name} onChart`).toBeGreaterThan(5);
      expect(item.howToRead.length).withContext(`${g}/${item.name} howToRead`).toBeGreaterThanOrEqual(2);
      expect(item.howToRead.length).withContext(`${g}/${item.name} howToRead`).toBeLessThanOrEqual(4);
      expect(item.pitfall.length).withContext(`${g}/${item.name} pitfall`).toBeGreaterThan(10);
    }));
  });

  it('no unexplained jargon: smart money and squeeze never appear', () => {
    const all = JSON.stringify(INDICATOR_GUIDES).toLowerCase();
    expect(all).not.toContain('smart money');
    expect(all).not.toContain('squeeze');
  });

  it('covers the indicators the chart actually draws', () => {
    const names = (g: TaGroup) => INDICATOR_GUIDES[g].items.map(i => i.name).join(' | ');
    expect(names('xu-huong')).toContain('EMA');
    expect(names('xu-huong')).toContain('ADX');
    expect(names('dong-luong')).toContain('RSI');
    expect(names('dong-luong')).toContain('MACD');
    expect(names('dong-luong')).toContain('Stochastic');
    expect(names('bien-dong')).toContain('Bollinger');
    expect(names('bien-dong')).toContain('ATR');
    expect(names('dong-tien')).toContain('Khối lượng');
    expect(names('dong-tien')).toContain('OBV');
    expect(names('dong-tien')).toContain('MFI');
    expect(names('moc-gia')).toContain('Hỗ trợ');
    expect(names('moc-gia')).toContain('Fibonacci');
  });
});

describe('readNow', () => {
  it('readNow rsi above 70 says overbought', () => {
    const out = readNow('dong-luong', series({ rsi14: withLast(50, 72) }), analysis());

    expect(out).toContain('RSI đang 72, trên 70: giá đã tăng nóng, dễ điều chỉnh.');
  });

  it('rsi below 30 says oversold', () => {
    const out = readNow('dong-luong', series({ rsi14: withLast(50, 25) }), analysis());

    expect(out.some(s => s.startsWith('RSI đang 25, dưới 30') && s.includes('giảm sâu'))).toBeTrue();
  });

  it('rsi in the middle says neither hot nor cold', () => {
    const out = readNow('dong-luong', series({ rsi14: withLast(50, 55) }), analysis());

    expect(out.some(s => s.startsWith('RSI đang 55') && s.includes('giữa 30 và 70'))).toBeTrue();
  });

  it('readNow omits sentence when value null', () => {
    const out = readNow('dong-luong', series({ rsi14: withLast(50, null), macdLine: withLast(0, 1) }), analysis());

    expect(out.some(s => s.includes('RSI'))).toBeFalse();
    expect(out.some(s => s.includes('MACD'))).toBeTrue();   // các câu khác vẫn còn
  });

  it('never throws and never guesses when every indicator is null', () => {
    const empty = series({
      ema20: flat(null), ema50: flat(null), bollingerUpper: flat(null), bollingerMiddle: flat(null), bollingerLower: flat(null),
      rsi14: flat(null), stochK: flat(null), stochD: flat(null), macdLine: flat(null), macdSignal: flat(null), macdHistogram: flat(null),
      adx: flat(null), plusDi: flat(null), minusDi: flat(null), atr14: flat(null), obv: flat(null), mfi14: flat(null), volumeSma20: flat(null),
    });
    groups.filter(g => g !== 'tong-hop' && g !== 'moc-gia').forEach(g => {
      expect(readNow(g, empty, analysis())).withContext(g).toEqual([]);
    });
  });

  it('readNow macd above signal', () => {
    const out = readNow('dong-luong', series({ macdLine: withLast(0, 1.5), macdSignal: withLast(0, 1) }), analysis());

    expect(out).toContain('MACD đang trên đường tín hiệu: động lượng nghiêng về tăng.');
  });

  it('macd below signal says the momentum leans down', () => {
    const out = readNow('dong-luong', series({ macdLine: withLast(0, -1), macdSignal: withLast(0, 1) }), analysis());

    expect(out).toContain('MACD đang dưới đường tín hiệu: động lượng nghiêng về giảm.');
  });

  it('ema20 above ema50 says the short-term trend is up', () => {
    const out = readNow('xu-huong', series({ ema20: withLast(100, 105), ema50: withLast(100, 100) }), analysis());

    expect(out).toContain('EMA20 trên EMA50: xu hướng ngắn hạn đang tăng.');
  });

  it('ema20 below ema50 says the short-term trend is down', () => {
    const out = readNow('xu-huong', series({ ema20: withLast(100, 95), ema50: withLast(100, 100) }), analysis());

    expect(out).toContain('EMA20 dưới EMA50: xu hướng ngắn hạn đang giảm.');
  });

  it('adx under 25 says there is no clear trend, over 25 says there is', () => {
    expect(readNow('xu-huong', series({ adx: withLast(20, 18) }), analysis()))
      .toContain('ADX 18, dưới 25: chưa có xu hướng rõ.');
    expect(readNow('xu-huong', series({ adx: withLast(20, 31) }), analysis()))
      .toContain('ADX 31, từ 25 trở lên: xu hướng đã rõ.');
  });

  it('close above the upper Bollinger band is called out', () => {
    const out = readNow('bien-dong', series({ close: withLast(100, 112) as number[] }), analysis());

    expect(out).toContain('Giá đóng cửa trên dải Bollinger trên: giá đang đi xa mức trung bình, dễ chững lại hoặc quay về.');
  });

  it('close inside the bands says so', () => {
    const out = readNow('bien-dong', series(), analysis());

    expect(out.some(s => s.includes('nằm trong dải Bollinger'))).toBeTrue();
  });

  it('volume ratio against the 20-session average is spelled out with a Vietnamese decimal comma', () => {
    const out = readNow('dong-tien', series({ volume: withLast(1000, 2300) as number[], volumeSma20: flat(1000) }), analysis());

    expect(out).toContain('Khối lượng phiên cuối gấp 2,3 lần trung bình 20 phiên.');
  });

  it('the wording follows the rounded ratio so the sentence never contradicts the number', () => {
    const out = readNow('dong-tien', series({ volume: withLast(1000, 1490) as number[], volumeSma20: flat(1000) }), analysis());

    expect(out).toContain('Khối lượng phiên cuối gấp 1,5 lần trung bình 20 phiên.');
  });

  it('volume below average says it is thin', () => {
    const out = readNow('dong-tien', series({ volume: withLast(1000, 500) as number[], volumeSma20: flat(1000) }), analysis());

    expect(out.some(s => s.includes('0,5 lần') && s.includes('thấp'))).toBeTrue();
  });

  it('obv rising over the last sessions is read as money coming in', () => {
    const rising = Array.from({ length: N }, (_, i) => 1000 + i * 10);
    const out = readNow('dong-tien', series({ obv: rising }), analysis());

    expect(out.some(s => s.startsWith('OBV') && s.includes('đi lên'))).toBeTrue();
  });

  it('mfi above 80 or below 20 is called out', () => {
    expect(readNow('dong-tien', series({ mfi14: withLast(50, 85) }), analysis()).some(s => s.includes('MFI') && s.includes('trên 80'))).toBeTrue();
    expect(readNow('dong-tien', series({ mfi14: withLast(50, 15) }), analysis()).some(s => s.includes('MFI') && s.includes('dưới 20'))).toBeTrue();
  });

  it('moc-gia names the nearest support below and resistance above the price', () => {
    const out = readNow('moc-gia', series(), analysis({ currentPrice: 100, supportLevels: [90, 95, 80], resistanceLevels: [110, 105] }));

    expect(out.some(s => s.startsWith('Hỗ trợ gần nhất: 95') && s.includes('5,0%'))).toBeTrue();
    expect(out.some(s => s.startsWith('Kháng cự gần nhất: 105') && s.includes('5,0%'))).toBeTrue();
  });

  it('moc-gia without levels says nothing', () => {
    expect(readNow('moc-gia', series(), analysis({ supportLevels: [], resistanceLevels: [] }))).toEqual([]);
  });

  it('tong-hop reads the confluence score and the suggested prices', () => {
    const out = readNow('tong-hop', series(), analysis());

    expect(out.some(s => s.includes('68,5') && s.includes('nghiêng về mua'))).toBeTrue();
    expect(out.some(s => s.includes('vào') && s.includes('cắt lỗ') && s.includes('chốt lời'))).toBeTrue();
  });

  it('tong-hop without a confluence score or suggestion says nothing', () => {
    const out = readNow('tong-hop', series(), analysis({
      confluenceScore: undefined, suggestedEntry: undefined, suggestedStopLoss: undefined, suggestedTarget: undefined,
    }));

    expect(out).toEqual([]);
  });
});
