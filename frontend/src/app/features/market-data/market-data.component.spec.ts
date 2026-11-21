import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { MarketDataComponent } from './market-data.component';
import { MarketDataService, TechnicalAnalysis } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';

// Stub đủ mọi trường (kể cả fibonacci, ema200) — từng test ghi đè phần cần thiếu.
function fullTa(): TechnicalAnalysis {
  return {
    symbol: 'HPG',
    analyzedAt: '2026-11-21T00:00:00Z',
    dataPoints: 250,
    currentPrice: 28000,
    priceChange: 300,
    priceChangePercent: 1.08,
    currentVolume: 1_200_000,
    ema20: 27500,
    ema50: 27000,
    emaTrend: 'bullish',
    rsi14: 55.2,
    rsiSignal: 'neutral',
    macdLine: 120,
    signalLine: 100,
    macdHistogram: 20,
    macdSignal: 'buy',
    avgVolume20: 1_000_000,
    volumeRatio: 1.2,
    volumeSignal: 'normal',
    supportLevels: [26500, 27000],
    resistanceLevels: [28500, 29000],
    overallSignal: 'buy',
    overallSignalVi: 'MUA',
    bullishCount: 5,
    bearishCount: 2,
    neutralCount: 3,
    bollingerUpper: 29000,
    bollingerMiddle: 27500,
    bollingerLower: 26000,
    bollingerBandwidth: 10.9,
    bollingerPercentB: 0.667,
    bollingerSignal: 'neutral',
    ema200: 25000,
    ema21: 27400,
    highestHigh22: 29200,
    lowestLow22: 26100,
    fibonacci: {
      swingHigh: 29200,
      swingLow: 26100,
      retracement236: 28468,
      retracement382: 28016,
      retracement500: 27650,
      retracement618: 27284,
      retracement786: 26763,
      extension1272: 30043,
      extension1618: 31116,
    },
    atr14: 550,
    atrPercent: 1.96,
    stochasticK: 62.1,
    stochasticD: 58.4,
    stochasticSignal: 'neutral',
    adx14: 28.3,
    plusDi: 24.1,
    minusDi: 15.2,
    adxSignal: 'trending',
    obv: 15_300_000,
    obvSignal: 'rising',
    mfi14: 61.5,
    mfiSignal: 'neutral',
    confluenceScore: 68.5,
    marketCondition: 'trending',
    marketConditionVi: 'Có xu hướng',
    suggestedStrategy: 'Giao dịch theo xu hướng',
    rsiDivergence: undefined,
    macdDivergence: undefined,
    divergenceSignal: undefined,
    divergenceSignalVi: undefined,
    suggestedEntry: 27800,
    suggestedStopLoss: 26800,
    suggestedTarget: 29800,
    riskRewardRatio: 2.0,
  };
}

describe('MarketDataComponent — panel phân tích kỹ thuật theo 6 khối', () => {
  let fixture: ComponentFixture<MarketDataComponent>;

  function setup(analysis: TechnicalAnalysis | null) {
    const marketSpy = jasmine.createSpyObj<MarketDataService>('MarketDataService', [
      'getMarketOverview', 'getBatchPrices', 'getTopFluctuation',
    ]);
    marketSpy.getMarketOverview.and.returnValue(of([]));
    marketSpy.getBatchPrices.and.returnValue(of([]));
    marketSpy.getTopFluctuation.and.returnValue(of([]));
    const notifySpy = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']);
    const dossierSpy = jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']);

    TestBed.configureTestingModule({
      imports: [MarketDataComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MarketDataService, useValue: marketSpy },
        { provide: NotificationService, useValue: notifySpy },
        { provide: CompanyDossierService, useValue: dossierSpy },
      ],
    });
    fixture = TestBed.createComponent(MarketDataComponent);
    fixture.componentInstance.analysis = analysis;
    fixture.detectChanges();
  }

  function groupValues(): (string | null)[] {
    return fixture.debugElement.queryAll(By.css('[data-group]'))
      .map(el => el.nativeElement.getAttribute('data-group'));
  }

  it('6 tiêu đề khối render đúng thứ tự', () => {
    setup(fullTa());

    expect(groupValues()).toEqual([
      'tong-hop', 'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia',
    ]);
  });

  it('không có analysis thì không có tiêu đề khối nào', () => {
    setup(null);

    expect(groupValues().length).toBe(0);
  });

  it('confluence null và không có suggestedEntry thì ẩn khối Tổng hợp', () => {
    const ta = fullTa();
    ta.confluenceScore = undefined;
    ta.suggestedEntry = undefined;
    setup(ta);

    expect(groupValues()).toEqual([
      'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia',
    ]);
  });

  it('hỗ trợ/kháng cự rỗng vẫn hiện 2 thẻ với Chưa xác định', () => {
    const ta = fullTa();
    ta.supportLevels = [];
    ta.resistanceLevels = [];
    setup(ta);

    const mocGia = fixture.debugElement.query(By.css('[data-group="moc-gia"]'));
    expect(mocGia).toBeTruthy();
    const text: string = fixture.nativeElement.textContent;
    const matches = text.match(/Chưa xác định/g) ?? [];
    expect(matches.length).toBeGreaterThanOrEqual(2);
    expect(text).toContain('Hỗ trợ');
    expect(text).toContain('Kháng cự');
  });

  it('marketCondition unknown thì ẩn thẻ Trạng thái nhưng nhóm Xu hướng vẫn còn', () => {
    const ta = fullTa();
    ta.marketCondition = 'unknown';
    setup(ta);

    expect(fixture.debugElement.query(By.css('[data-group="xu-huong"]'))).toBeTruthy();
    const text: string = fixture.nativeElement.textContent;
    expect(text).not.toContain('Trạng thái thị trường');
  });

  function normalizedText(): string {
    return (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
  }

  it('có ema200 thì thẻ EMA hiện giá trị EMA200', () => {
    setup(fullTa());

    const text = normalizedText();
    // toContain vì tiêu đề thật có emoji 📊 phía trước
    expect(text).toContain('Xu hướng (EMA20/50/200)');
    expect(text).toContain('EMA200:');
    expect(text).not.toContain('EMA200: —');
  });

  it('thiếu ema200 thì hiện EMA200: —', () => {
    const ta = fullTa();
    ta.ema200 = undefined;
    setup(ta);

    expect(normalizedText()).toContain('EMA200: —');
  });

  it('có fibonacci thì thẻ Fibonacci hiện 5 mốc retracement', () => {
    setup(fullTa());

    const levels = fixture.debugElement.queryAll(By.css('[data-fib-level]'));
    expect(levels.length).toBe(5);
    const labels = levels.map(l => (l.nativeElement.textContent as string));
    for (const expected of ['23.6%', '38.2%', '50%', '61.8%', '78.6%']) {
      expect(labels.some(t => t.includes(expected)))
        .withContext(`thiếu mốc ${expected}`).toBeTrue();
    }
  });

  it('thiếu fibonacci thì thẻ hiện Không đủ dữ liệu', () => {
    const ta = fullTa();
    ta.fibonacci = undefined;
    setup(ta);

    const fibCard = fixture.debugElement.query(By.css('[data-card="fibonacci"]'));
    expect(fibCard).toBeTruthy();
    expect(fibCard.nativeElement.textContent).toContain('Không đủ dữ liệu');
    expect(fixture.debugElement.queryAll(By.css('[data-fib-level]')).length).toBe(0);
  });
});
