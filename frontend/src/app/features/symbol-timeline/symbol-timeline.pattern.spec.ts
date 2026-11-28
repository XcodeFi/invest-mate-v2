import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { BehaviorSubject, of } from 'rxjs';
import { SymbolTimelineComponent } from './symbol-timeline.component';
import { JournalEntryService } from '../../core/services/journal-entry.service';
import { MarketDataService, PatternCandidate, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { MarketEventService } from '../../core/services/market-event.service';
import { NotificationService } from '../../core/services/notification.service';

const N = 60;
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const daysAgo = (n: number) => { const d = new Date(); d.setDate(d.getDate() - n); return d; };
const flat = (v: number) => Array.from({ length: N }, () => v);
const dates = Array.from({ length: N }, (_, i) => `${iso(daysAgo(N - 1 - i))}T00:00:00Z`);

const series = (): TechnicalSeries => ({
  adjusted: true, symbol: 'VNM', dates,
  open: flat(100), high: flat(101), low: flat(99), close: flat(100), volume: flat(1000),
  ema20: flat(112), ema50: flat(100), ema200: flat(98), bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
  rsi14: flat(72), stochK: flat(50), stochD: flat(50), macdLine: flat(2), macdSignal: flat(1), macdHistogram: flat(1),
  adx: flat(18), plusDi: flat(20), minusDi: flat(15), atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
});

const cup: PatternCandidate = {
  type: 'cup-with-handle', typeVi: 'Cốc tay cầm', state: 'forming', stateVi: 'Đang hình thành',
  startDate: dates[5], bottomDate: dates[20], rightLipDate: dates[45],
  cupLengthWeeks: 8, cupDepthPct: 20, handleLengthSessions: 14, handleDepthPct: 9,
  pivotPrice: 108, distanceToPivotPct: 8, volumeVsAvg50: 1, stopPrice: 97, riskPct: 10.2, targetPrice: 126, rewardPct: 16.7, rewardRisk: 1.6,
  criteria: [], disclaimer: 'không phải tín hiệu mua',
};

describe('SymbolTimelineComponent: ứng viên mô hình Cốc tay cầm', () => {
  let fixture: ComponentFixture<SymbolTimelineComponent>;
  let component: SymbolTimelineComponent;

  function setup(withCup: boolean) {
    const route = { snapshot: { paramMap: convertToParamMap({ symbol: 'vnm' }), queryParams: {} }, queryParamMap: new BehaviorSubject(convertToParamMap({})).asObservable() } as any;
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getTechnicalSeries', 'getTechnicalAnalysis']);
    market.getTechnicalSeries.and.callFake(() => of(series()));
    market.getTechnicalAnalysis.and.callFake(() => of({
      symbol: 'VNM', currentPrice: 100, supportLevels: [95], resistanceLevels: [105], suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 110,
      overallSignal: 'buy', overallSignalVi: 'MUA', ...(withCup ? { patternCandidates: [cup] } : {}),
    } as unknown as TechnicalAnalysis));
    const journal = jasmine.createSpyObj<JournalEntryService>('JournalEntryService', ['getTimeline', 'create']);
    journal.getTimeline.and.callFake(() => of({ symbol: 'VNM', items: [], holdingPeriods: [], emotionSummary: null, behavioralPatterns: [] } as any));
    TestBed.configureTestingModule({
      imports: [SymbolTimelineComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: route },
        { provide: MarketDataService, useValue: market },
        { provide: JournalEntryService, useValue: journal },
        { provide: MarketEventService, useValue: jasmine.createSpyObj<MarketEventService>('MarketEventService', ['create']) },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success', 'warning']) },
      ],
    });
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(SymbolTimelineComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    fixture.detectChanges();
  }

  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);
  const labels = () => component.groupLayers.priceLines.map(p => p.label);

  afterEach(() => fixture?.destroy());

  it('shows the candidate card and draws pivot, stop and target on the chart even with no indicator group chosen', () => {
    setup(true);

    expect(q('pattern-card')).not.toBeNull();
    expect(labels().some(l => l.startsWith('Pivot 108'))).toBeTrue();
    expect(labels().some(l => l.includes('−10,2% từ pivot'))).toBeTrue();
    expect(labels().some(l => l.startsWith('Mục tiêu đo 126'))).toBeTrue();
    expect(component.groupLayers.overlays.map(o => o.label)).toContain('Tay cầm');
  });

  it('no candidate: no card and no pattern line', () => {
    setup(false);

    expect(q('pattern-card')).toBeNull();
    expect(labels().some(l => l.startsWith('Pivot'))).toBeFalse();
  });

  it('the checkbox turns the drawing off and on again', () => {
    setup(true);
    const box = q('pattern-show') as HTMLInputElement;

    box.checked = false;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(labels().some(l => l.startsWith('Pivot'))).toBeFalse();

    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(labels().some(l => l.startsWith('Pivot'))).toBeTrue();
  });
});
