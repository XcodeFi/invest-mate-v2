import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { WatchlistService } from './watchlist.service';

describe('WatchlistService.addItemsBulk', () => {
  let service: WatchlistService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { getToken: () => 'tok' } }],
    });
    service = TestBed.inject(WatchlistService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('posts to {id}/items/bulk with { symbols, note } and the bearer token, and returns the server result', () => {
    let got: unknown;
    service.addItemsBulk('w1', ['AAA', 'BBB'], 'Từ sàng lọc 28/11').subscribe(r => (got = r));

    const req = http.expectOne(`${environment.apiUrl}/watchlists/w1/items/bulk`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ symbols: ['AAA', 'BBB'], note: 'Từ sàng lọc 28/11' });
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok');
    req.flush({ watchlistId: 'w1', added: ['AAA'], skipped: ['BBB'] });

    expect(got).toEqual({ watchlistId: 'w1', added: ['AAA'], skipped: ['BBB'] });
  });

  it('does not hit the single-item endpoint', () => {
    service.addItemsBulk('w1', ['AAA']).subscribe();

    const req = http.expectOne(r => r.url.endsWith('/items/bulk'));
    expect(req.request.url.endsWith('/items')).toBeFalse();
    req.flush({ watchlistId: 'w1', added: [], skipped: [] });
  });
});
