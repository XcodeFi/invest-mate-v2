import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PositionSizingRequest, RiskService } from './risk.service';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

describe('RiskService.calculatePositionSizing', () => {
  let service: RiskService;
  let http: HttpTestingController;
  const request: PositionSizingRequest = {
    accountBalance: 1_000_000_000, entryPrice: 100_000, stopLoss: 95_000, riskPercent: 1, maxPositionPercent: 20, atrMultiplier: 2,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(), RiskService,
        { provide: AuthService, useValue: { getToken: () => 'test-token' } },
      ],
    });
    service = TestBed.inject(RiskService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends no query parameters by default (old behaviour unchanged)', () => {
    service.calculatePositionSizing(request).subscribe();

    const req = http.expectOne(r => r.url === `${environment.apiUrl}/risk/position-sizing`);
    expect(req.request.method).toBe('POST');
    expect(req.request.params.keys().length).toBe(0);
    expect(req.request.body).toEqual(request);
    req.flush({ models: [], recommendedModel: 'fixed_risk' });
  });

  it('adds useHistory and the strategy id when asked', () => {
    service.calculatePositionSizing(request, { useHistory: true, strategyId: 's1' }).subscribe();

    const req = http.expectOne(r => r.url === `${environment.apiUrl}/risk/position-sizing`);
    expect(req.request.params.get('useHistory')).toBe('true');
    expect(req.request.params.get('strategyId')).toBe('s1');
    req.flush({ models: [], recommendedModel: 'fixed_risk' });
  });

  it('ignores the strategy id when useHistory is off', () => {
    service.calculatePositionSizing(request, { useHistory: false, strategyId: 's1' }).subscribe();

    const req = http.expectOne(r => r.url === `${environment.apiUrl}/risk/position-sizing`);
    expect(req.request.params.keys().length).toBe(0);
    req.flush({ models: [], recommendedModel: 'fixed_risk' });
  });
});
