import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StrategyExpectancyDto, TradePlanService } from './trade-plan.service';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

/** Project không có interceptor auth toàn cục — mọi service phải tự gắn Authorization. */
describe('TradePlanService', () => {
  let service: TradePlanService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        TradePlanService,
        { provide: AuthService, useValue: { getToken: () => 'test-token' } },
      ],
    });
    service = TestBed.inject(TradePlanService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getExpectancy calls trade-plans/expectancy with the auth header', () => {
    let received: StrategyExpectancyDto | undefined;
    service.getExpectancy().subscribe(r => (received = r));

    const req = http.expectOne(`${environment.apiUrl}/trade-plans/expectancy`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');

    const body = { total: { n: 0 }, strategies: [] } as unknown as StrategyExpectancyDto;
    req.flush(body);
    expect(received).toEqual(body);
  });
});
