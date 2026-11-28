import { routes } from '../../app.routes';
import { allNavItems, CHART_ROUTE_PREFIXES, isChartRoute, NAV_EXCLUDED_ROUTES, NAV_GROUPS } from './nav-config';

describe('nav-config: một cấu hình duy nhất cho menu trái và menu điện thoại', () => {
  const routePaths = routes.map(r => r.path).filter((p): p is string => p !== undefined);
  /** Route chỉ chuyển hướng sang trang khác ('/position-sizing' → '/trade-plan'): không phải trang riêng nên không có mục menu. */
  const redirectRoutes = routes.filter(r => r.redirectTo !== undefined).map(r => r.path!);
  const nonParamRoutes = routePaths.filter(p => !p.includes(':') && !redirectRoutes.includes(p));
  const navPaths = allNavItems().map(i => i.path);

  it('covers_every_non_param_route (a new route must be added to the nav config or to the excluded list)', () => {
    const missing = nonParamRoutes.filter(p => !NAV_EXCLUDED_ROUTES.includes(p) && !navPaths.includes('/' + p));

    expect(missing).withContext('route chưa có trong menu: thêm vào NAV_GROUPS (hoặc NAV_EXCLUDED_ROUTES kèm lý do)').toEqual([]);
  });

  it('has_no_redirect_items (a menu item must not point to a route that only redirects, or the page shows twice)', () => {
    const duplicates = navPaths.filter(p => redirectRoutes.includes(p.substring(1)));

    expect(duplicates).withContext('mục menu trỏ tới route chỉ chuyển hướng: bỏ mục, trang đích đã có mục').toEqual([]);
    expect(redirectRoutes).withContext('test này phải thấy các route chuyển hướng thật').toContain('position-sizing');
    expect(redirectRoutes).toContain('advanced-analytics');
  });

  it('has_no_dead_links (every menu path is a real route)', () => {
    const dead = navPaths.filter(p => !routePaths.includes(p.substring(1)));

    expect(dead).withContext('mục menu trỏ tới route không tồn tại').toEqual([]);
  });

  it('never lists a symbol-param route', () => {
    expect(navPaths.filter(p => p.includes(':'))).toEqual([]);
  });

  it('lists each page once', () => {
    expect(new Set(navPaths).size).toBe(navPaths.length);
  });

  it('the excluded routes are exactly the ones that are not pages of the menu', () => {
    expect([...NAV_EXCLUDED_ROUTES].sort()).toEqual(['', '**', 'admin', 'auth', 'changelog']);
    NAV_EXCLUDED_ROUTES.forEach(p => expect(routePaths).withContext(p).toContain(p));
  });

  it('groups are by moment of work, in this order, with Cài đặt last and collapsed by default', () => {
    expect(NAV_GROUPS.map(g => g.label)).toEqual(['HÔM NAY', 'TÌM CƠ HỘI', 'KẾ HOẠCH & GD', 'ĐÁNH GIÁ', 'TIỀN & TÀI SẢN', 'Cài đặt']);
    expect(NAV_GROUPS.filter(g => g.collapsedByDefault).map(g => g.label)).toEqual(['Cài đặt']);
  });

  it('puts the headline pages in their groups', () => {
    const group = (path: string) => NAV_GROUPS.find(g => g.items.some(i => i.path === path))?.label;

    expect(group('/dashboard')).toBe('HÔM NAY');
    expect(group('/daily-routine')).toBe('HÔM NAY');
    expect(group('/alerts')).toBe('HÔM NAY');
    expect(group('/market-data')).toBe('TÌM CƠ HỘI');
    expect(group('/watchlist')).toBe('TÌM CƠ HỘI');
    expect(group('/company-dossier')).toBe('TÌM CƠ HỘI');
    expect(group('/screener')).toBe('TÌM CƠ HỘI');
    expect(group('/trade-plan')).toBe('KẾ HOẠCH & GD');
    expect(group('/trades')).toBe('KẾ HOẠCH & GD');
    expect(group('/positions')).toBe('KẾ HOẠCH & GD');
    expect(group('/pending-reviews')).toBe('ĐÁNH GIÁ');
    expect(group('/campaign-analytics')).toBe('ĐÁNH GIÁ');
    expect(group('/risk-dashboard')).toBe('ĐÁNH GIÁ');
    expect(group('/monthly-review')).toBe('ĐÁNH GIÁ');
    expect(group('/portfolios')).toBe('TIỀN & TÀI SẢN');
    expect(group('/capital-flows')).toBe('TIỀN & TÀI SẢN');
    expect(group('/personal-finance')).toBe('TIỀN & TÀI SẢN');
    expect(group('/ai-settings')).toBe('Cài đặt');
    expect(group('/api-keys')).toBe('Cài đặt');
  });

  it('every item has a label, an icon and a path starting with a slash', () => {
    allNavItems().forEach(i => {
      expect(i.label.trim()).withContext(i.path).not.toBe('');
      expect(i.icon.trim()).withContext(i.path).not.toBe('');
      expect(i.path.startsWith('/')).withContext(i.path).toBeTrue();
    });
  });

  it('user-facing text keeps full Vietnamese diacritics (no unaccented labels)', () => {
    const unaccented = ['Tong quan', 'Nhiem vu', 'Canh bao', 'Thi truong', 'Ke hoach', 'Giao dich', 'Vi the', 'Cai dat'];
    const text = NAV_GROUPS.flatMap(g => [g.label, ...g.items.map(i => i.label)]).join('|');

    unaccented.forEach(u => expect(text).withContext(u).not.toContain(u));
  });
});

describe('isChartRoute: trang nhiều biểu đồ mặc định thu gọn menu', () => {
  it('knows the chart routes', () => {
    expect([...CHART_ROUTE_PREFIXES]).toEqual(['/symbol-timeline', '/trade-plan', '/market-data', '/trade-replay']);
  });

  it('matches a chart route, its sub-paths, params and query strings', () => {
    expect(isChartRoute('/symbol-timeline')).toBeTrue();
    expect(isChartRoute('/symbol-timeline/FPT')).toBeTrue();
    expect(isChartRoute('/symbol-timeline/FPT?range=6T&grp=momentum')).toBeTrue();
    expect(isChartRoute('/trade-plan')).toBeTrue();
    expect(isChartRoute('/trade-plan?x=1')).toBeTrue();
    expect(isChartRoute('/market-data#top')).toBeTrue();
    expect(isChartRoute('/trade-replay/abc123')).toBeTrue();
  });

  it('does not match other pages, including look-alike prefixes', () => {
    expect(isChartRoute('/dashboard')).toBeFalse();
    expect(isChartRoute('/positions')).toBeFalse();
    expect(isChartRoute('/trade-planner')).toBeFalse();
    expect(isChartRoute('/market-data-x')).toBeFalse();
    expect(isChartRoute('/trades')).toBeFalse();
    expect(isChartRoute('')).toBeFalse();
  });
});
