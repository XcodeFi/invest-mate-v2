import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MarketDataService, ScreenerResult, SignalSetups, TechnicalSeries } from './market-data.service';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

/**
 * Project không có interceptor auth toàn cục — mọi service phải tự gắn Authorization.
 */
describe('MarketDataService', () => {
  let service: MarketDataService;
  let http: HttpTestingController;

  const authStub = { getToken: () => 'test-token' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MarketDataService,
        { provide: AuthService, useValue: authStub }
      ]
    });
    service = TestBed.inject(MarketDataService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getSignalSetups calls {symbol}/signal-setups with auth header', () => {
    let received: SignalSetups | undefined;
    service.getSignalSetups('FPT').subscribe(s => (received = s));

    const req = http.expectOne(`${environment.apiUrl}/market/FPT/signal-setups`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    req.flush({ symbol: 'FPT', adjusted: true, setups: [], disclaimer: 'x', notes: [] });
    expect(received?.symbol).toBe('FPT');
  });

  it('getTechnicalSeries calls analysis/series with auth header', () => {
    let received: TechnicalSeries | undefined;
    service.getTechnicalSeries('FPT').subscribe(s => (received = s));

    const req = http.expectOne(`${environment.apiUrl}/market/stock/FPT/analysis/series`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.has('Authorization')).toBeTrue();
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');

    const body = { symbol: 'FPT', dates: ['2026-11-24T00:00:00'], close: [100], ema20: [null] } as unknown as TechnicalSeries;
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('getTechnicalSeries and getTechnicalAnalysis send months only when asked', () => {
    service.getTechnicalSeries('FPT', 24).subscribe();
    const withMonths = http.expectOne(r => r.url === `${environment.apiUrl}/market/stock/FPT/analysis/series`);
    expect(withMonths.request.params.get('months')).toBe('24');
    withMonths.flush({});

    service.getTechnicalSeries('FPT').subscribe();
    const without = http.expectOne(r => r.url === `${environment.apiUrl}/market/stock/FPT/analysis/series`);
    expect(without.request.params.has('months')).toBeFalse();
    without.flush({});

    service.getTechnicalAnalysis('FPT', 15).subscribe();
    const analysis = http.expectOne(r => r.url === `${environment.apiUrl}/market/stock/FPT/analysis`);
    expect(analysis.request.params.get('months')).toBe('15');
    analysis.flush({});
  });
  it('getScreener calls market/screener with auth and sends only the criteria that are set', () => {
    let received: ScreenerResult | undefined;
    service.getScreener({ floors: 'HOSE,HNX', includeUpcom: false, minAdtv: 8, maxPe: undefined, lookupSymbol: '', skipQualityLayer: true })
      .subscribe(r => (received = r));

    const req = http.expectOne(r => r.url === `${environment.apiUrl}/market/screener`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    expect(req.request.params.get('floors')).toBe('HOSE,HNX');
    expect(req.request.params.get('includeUpcom')).toBe('false');
    expect(req.request.params.get('minAdtv')).toBe('8');
    expect(req.request.params.get('skipQualityLayer')).toBe('true');
    expect(req.request.params.has('maxPe')).toBeFalse();
    expect(req.request.params.has('lookupSymbol')).toBeFalse();
    req.flush({ status: 'ok', passed: [] });
    expect(received?.status).toBe('ok');
  });
});
