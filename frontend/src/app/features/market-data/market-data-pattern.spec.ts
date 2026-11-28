import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { MarketDataComponent } from './market-data.component';
import { MarketDataService, PatternCandidate, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';
import { AuthService } from '../../core/services/auth.service';

const N = 40;
const flat = (v: number): number[] => Array.from({ length: N }, () => v);
const dates = Array.from({ length: N }, (_, i) => new Date(Date.UTC(2026, 0, 1 + i)).toISOString().slice(0, 10));

const series = (): TechnicalSeries => ({
  adjusted: true, symbol: 'HPG', dates,
  open: flat(100), high: flat(101), low: flat(99), close: flat(100), volume: flat(1000),
  ema20: flat(105), ema50: flat(100), bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
  rsi14: flat(72), stochK: flat(50), stochD: flat(50), macdLine: flat(2), macdSignal: flat(1), macdHistogram: flat(1),
  adx: flat(18), plusDi: flat(20), minusDi: flat(15), atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
});

const cup: PatternCandidate = {
  type: 'cup-with-handle', typeVi: 'Cốc tay cầm', state: 'near-pivot', stateVi: 'Sát pivot',
  startDate: '2026-01-02', bottomDate: '2026-01-10', rightLipDate: '2026-01-20',
  cupLengthWeeks: 22, cupDepthPct: 21.5, handleLengthSessions: 8, handleDepthPct: 9.4,
  pivotPrice: 108, distanceToPivotPct: 2.1, volumeVsAvg50: 0.8, stopPrice: 97.5, riskPct: 9.7, targetPrice: 125, rewardPct: 15.7, rewardRisk: 1.6,
  criteria: [{ key: 'cup-depth', rule: 'Cốc sâu 12–33%', value: 21.5, unit: '%', passed: true, required: true }], disclaimer: 'không phải tín hiệu mua',
};

const analysis = (withCup: boolean): TechnicalAnalysis => ({
  symbol: 'HPG', currentPrice: 100, supportLevels: [95], resistanceLevels: [105],
  suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 110, confluenceScore: 68.5, overallSignal: 'buy', overallSignalVi: 'MUA',
  ...(withCup ? { patternCandidates: [cup] } : {}),
} as unknown as TechnicalAnalysis);

describe('MarketDataComponent: ứng viên mô hình Cốc tay cầm', () => {
  let fixture: ComponentFixture<MarketDataComponent>;
  let component: MarketDataComponent;

  function setup(withCup: boolean) {
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', [
      'getMarketOverview', 'getBatchPrices', 'getTopFluctuation', 'getTechnicalSeries', 'getTechnicalAnalysis', 'getSignalSetups',
    ]);
    market.getMarketOverview.and.returnValue(of([]));
    market.getBatchPrices.and.returnValue(of([]));
    market.getTopFluctuation.and.returnValue(of([]));
    market.getTechnicalSeries.and.returnValue(of(series()));
    market.getTechnicalAnalysis.and.returnValue(of(analysis(withCup)));
    market.getSignalSetups.and.returnValue(of({ symbol: 'HPG', adjusted: true, setups: [], disclaimer: 'x', notes: [] }));
    TestBed.configureTestingModule({
      imports: [MarketDataComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: MarketDataService, useValue: market },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']) },
        { provide: CompanyDossierService, useValue: jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']) },
        { provide: AuthService, useValue: { getToken: () => null } },
      ],
    });
    fixture = TestBed.createComponent(MarketDataComponent);
    component = fixture.componentInstance;
    component.analysis = analysis(withCup);
    fixture.detectChanges();
  }

  const click = (group: string) => {
    (fixture.debugElement.query(By.css(`[data-group="${group}"]`)).nativeElement as HTMLElement).click();
    fixture.detectChanges();
  };
  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);
  const labels = () => component.groupLayers.priceLines.map(p => p.label);

  afterEach(() => fixture?.destroy());

  it('shows the candidate card when the analysis carries a candidate', () => {
    setup(true);
    expect(q('pattern-card')).not.toBeNull();
  });

  it('shows no card without a candidate', () => {
    setup(false);
    expect(q('pattern-card')).toBeNull();
  });

  it('before any group is chosen there is no chart yet, so the card asks the user to pick a group', () => {
    setup(true);

    expect(q('pattern-chart-hint')).not.toBeNull();
    expect(labels()).toEqual([]);
  });

  it('once the chart loads the cup outline, pivot, stop and target are drawn together with the chosen group lines', () => {
    setup(true);

    click('tong-hop');

    expect(labels().some(l => l.startsWith('Giá vào'))).toBeTrue();
    expect(labels().some(l => l.startsWith('Pivot 108'))).toBeTrue();
    expect(labels().some(l => l.includes('−9,7% từ pivot'))).toBeTrue();
    expect(labels().some(l => l.startsWith('Mục tiêu đo 125'))).toBeTrue();
    expect(component.groupLayers.overlays.map(o => o.label)).toContain('Tay cầm');
    expect(q('pattern-chart-hint')).toBeNull();
  });

  it('switching the checkbox off removes the pattern lines but keeps the group lines; on brings them back', () => {
    setup(true);
    click('tong-hop');

    const box = q('pattern-show') as HTMLInputElement;
    box.checked = false;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(labels().some(l => l.startsWith('Pivot'))).toBeFalse();
    expect(labels().some(l => l.startsWith('Giá vào'))).toBeTrue();

    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(labels().some(l => l.startsWith('Pivot'))).toBeTrue();
  });

  it('without a candidate no pattern line is ever drawn', () => {
    setup(false);

    click('tong-hop');

    expect(labels().some(l => l.startsWith('Pivot'))).toBeFalse();
  });
});
