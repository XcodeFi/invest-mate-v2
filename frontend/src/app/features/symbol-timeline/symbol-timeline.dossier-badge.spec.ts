import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { SymbolTimelineComponent } from './symbol-timeline.component';
import { JournalEntryService } from '../../core/services/journal-entry.service';
import { MarketDataService } from '../../core/services/market-data.service';
import { MarketEventService } from '../../core/services/market-event.service';
import { NotificationService } from '../../core/services/notification.service';

const trade = (id: string, dossierAtTrade?: unknown) => ({
  type: 'trade', timestamp: new Date().toISOString(),
  data: { id, tradeType: 'BUY', quantity: 100, price: 25000, ...(dossierAtTrade === undefined ? {} : { dossierAtTrade }) }
});

describe('SymbolTimelineComponent — huy hiệu hồ sơ lúc đó trên giao dịch', () => {
  let fixture: ComponentFixture<SymbolTimelineComponent>;

  function setup(items: unknown[]) {
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getTechnicalSeries', 'getTechnicalAnalysis']);
    market.getTechnicalSeries.and.callFake(() => throwError(() => new Error('x')));
    market.getTechnicalAnalysis.and.callFake(() => throwError(() => new Error('x')));
    const journal = jasmine.createSpyObj<JournalEntryService>('JournalEntryService', ['getTimeline', 'create']);
    journal.getTimeline.and.returnValue(of({ symbol: 'HPG', items, holdingPeriods: [], emotionSummary: null, behavioralPatterns: [] } as any));
    TestBed.configureTestingModule({
      imports: [SymbolTimelineComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ symbol: 'hpg' }), queryParams: {} } } },
        { provide: MarketDataService, useValue: market },
        { provide: JournalEntryService, useValue: journal },
        { provide: MarketEventService, useValue: jasmine.createSpyObj<MarketEventService>('MarketEventService', ['create']) },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success', 'warning']) },
      ],
    });
    fixture = TestBed.createComponent(SymbolTimelineComponent);
    fixture.detectChanges();
    fixture.detectChanges();
  }

  const badges = () => Array.from(fixture.nativeElement.querySelectorAll('[data-testid="dossier-at-trade"]')) as HTMLElement[];

  afterEach(() => fixture?.destroy());

  it('a trade with no dossier shows no badge', () => {
    setup([trade('t1', null), trade('t2')]);

    expect(badges().length).toBe(0);
  });

  it('shows the signed badge with text and an icon', () => {
    setup([trade('t1', { state: 'signed', daysSigned: 95, freshness: 'needs-review' })]);

    expect(badges().length).toBe(1);
    expect(badges()[0].textContent).toContain('Hồ sơ ký 95 ngày trước lệnh · cần soát lại');
    expect(badges()[0].textContent).toContain('⚠️');
  });

  it('a re-signed dossier shows the date and no verdict, with the tooltip', () => {
    setup([trade('t1', { state: 'resigned-after', resignedAt: '2026-11-19T17:30:00Z' })]);

    expect(badges()[0].textContent).toContain('ký lại sau lệnh này (20/11)');
    expect(badges()[0].textContent).not.toContain('hết hạn');
    expect(badges()[0].getAttribute('title')).toContain('không được lưu');
  });

  it('each trade carries its own badge', () => {
    setup([trade('t1', { state: 'unsigned' }), trade('t2', { state: 'signed', daysSigned: 3, freshness: 'fresh' })]);

    expect(badges().length).toBe(2);
  });
});
