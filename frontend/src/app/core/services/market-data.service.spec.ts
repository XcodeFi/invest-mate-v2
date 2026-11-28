import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MarketDataService, SignalSetups, TechnicalSeries } from './market-data.service';
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
});
