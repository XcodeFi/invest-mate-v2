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
import { CompanyDossierService } from '../../core/services/company-dossier.service';

describe('SymbolTimelineComponent — thẻ Luận điểm cạnh biểu đồ', () => {
  let fixture: ComponentFixture<SymbolTimelineComponent>;
  let dossiers: jasmine.SpyObj<CompanyDossierService>;

  beforeEach(() => {
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getTechnicalSeries', 'getTechnicalAnalysis']);
    market.getTechnicalSeries.and.callFake(() => throwError(() => new Error('x')));
    market.getTechnicalAnalysis.and.callFake(() => throwError(() => new Error('x')));
    const journal = jasmine.createSpyObj<JournalEntryService>('JournalEntryService', ['getTimeline', 'create']);
    journal.getTimeline.and.returnValue(of({ symbol: 'HPG', items: [], holdingPeriods: [], emotionSummary: null, behavioralPatterns: [] } as any));
    dossiers = jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']);
    TestBed.configureTestingModule({
      imports: [SymbolTimelineComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ symbol: 'hpg' }), queryParams: {} } } },
        { provide: MarketDataService, useValue: market },
        { provide: JournalEntryService, useValue: journal },
        { provide: CompanyDossierService, useValue: dossiers },
        { provide: MarketEventService, useValue: jasmine.createSpyObj<MarketEventService>('MarketEventService', ['create']) },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success', 'warning']) },
      ],
    });
    fixture = TestBed.createComponent(SymbolTimelineComponent);
    fixture.detectChanges();
    fixture.detectChanges();
  });

  afterEach(() => fixture?.destroy());

  const q = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  it('shows the thesis card collapsed, and opening the page does not request the dossier', () => {
    expect(q('thesis-card')).not.toBeNull();
    expect(q('thesis-body')).toBeNull();
    expect(dossiers.get).not.toHaveBeenCalled();
  });

  it('the card carries the page symbol into the create-plan link', () => {
    expect(q('thesis-create-plan')!.getAttribute('href')).toBe('/trade-plan?symbol=HPG');
  });

  it('sits beside the chart above 1024px and stacks below it at 1024px and under', () => {
    const row = q('timeline-chart-row')!;

    expect(row.className).toContain('flex-col');
    expect(row.className).toContain('min-[1025px]:flex-row');
    expect(row.contains(q('timeline-chart-card'))).toBeTrue();
    expect(row.contains(q('thesis-card'))).toBeTrue();
  });
});
