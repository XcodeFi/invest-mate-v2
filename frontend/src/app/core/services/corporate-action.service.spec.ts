import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CorporateActionService } from './corporate-action.service';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

/**
 * Project không có interceptor auth toàn cục — mọi service phải tự gắn Authorization.
 * Thiếu header là 401 ở runtime mà build và unit test hàm thuần đều không phát hiện được.
 */
describe('CorporateActionService', () => {
  let service: CorporateActionService;
  let http: HttpTestingController;

  const authStub = { getToken: () => 'test-token' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        CorporateActionService,
        { provide: AuthService, useValue: authStub }
      ]
    });
    service = TestBed.inject(CorporateActionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getByPortfolio gắn Authorization', () => {
    service.getByPortfolio('p1').subscribe();

    const req = http.expectOne(`${environment.apiUrl}/corporate-actions/portfolio/p1`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    req.flush([]);
  });

  it('getByPortfolio truyền mã qua query param', () => {
    service.getByPortfolio('p1', 'HPG').subscribe();

    const req = http.expectOne(r => r.url === `${environment.apiUrl}/corporate-actions/portfolio/p1`);
    expect(req.request.params.get('symbol')).toBe('HPG');
    req.flush([]);
  });

  it('create gắn Authorization và gửi body PascalCase', () => {
    service.create({
      PortfolioId: 'p1', Symbol: 'HPG', Type: 'StockDividend',
      ExDate: '2026-06-10', RatioOld: 100, RatioNew: 130
    }).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/corporate-actions`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    expect(req.request.body.PortfolioId).toBe('p1');
    expect(req.request.body.RatioNew).toBe(130);
    req.flush({ id: 'a1' });
  });

  it('settle gắn Authorization', () => {
    service.settle('a1', '2026-07-20').subscribe();

    const req = http.expectOne(`${environment.apiUrl}/corporate-actions/a1/settle`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    expect(req.request.body.SettledAt).toBe('2026-07-20');
    req.flush(null);
  });

  it('delete gắn Authorization', () => {
    service.delete('a1').subscribe();

    const req = http.expectOne(`${environment.apiUrl}/corporate-actions/a1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    req.flush(null);
  });
});
