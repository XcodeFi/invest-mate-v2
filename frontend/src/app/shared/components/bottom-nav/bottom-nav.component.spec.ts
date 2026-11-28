import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BottomNavComponent } from './bottom-nav.component';
import { allNavItems, NAV_GROUPS } from '../../nav/nav-config';

describe('BottomNavComponent: thanh dưới và danh sách "Thêm" trên điện thoại', () => {
  let fixture: ComponentFixture<BottomNavComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [BottomNavComponent], providers: [provideRouter([])] });
    fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const openMore = () => {
    const more = Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').includes('Thêm')) as HTMLButtonElement;
    more.click();
    fixture.detectChanges();
  };
  const moreLinks = () => Array.from(el().querySelectorAll('.grid a')) as HTMLAnchorElement[];

  it('the "Thêm" list includes the screener page (its only entry point on mobile)', () => {
    openMore();

    const screener = moreLinks().find(a => a.getAttribute('href') === '/screener');
    expect(screener).toBeDefined();
    expect((screener!.textContent ?? '').trim()).toContain('Sàng lọc');
  });

  it('the "Thêm" list includes the mood journal (its only entry point on mobile besides the home hero link)', () => {
    openMore();

    const link = moreLinks().find(a => a.getAttribute('href') === '/mood-journal');
    expect(link).toBeDefined();
    expect((link!.textContent ?? '').trim()).toContain('Nhật ký tâm trạng');
  });

  it('the market page stays next to it', () => {
    openMore();

    expect(moreLinks().some(a => a.getAttribute('href') === '/market-data')).toBeTrue();
  });

  it('every "Thêm" link goes to a different page', () => {
    openMore();

    const hrefs = moreLinks().map(a => a.getAttribute('href'));
    expect(new Set(hrefs).size).toBe(hrefs.length);
  });

  const barPaths = () => Array.from(el().querySelectorAll('nav > div > a')).map(a => a.getAttribute('href'));

  it('the bar and the "Thêm" list together reach every page of NAV_GROUPS, with the same labels (one source of truth)', () => {
    openMore();

    const reachable = new Map<string, string>();
    moreLinks().forEach(a => reachable.set(a.getAttribute('href')!, (a.textContent ?? '').trim()));
    const bar = barPaths();
    allNavItems().forEach(item => {
      if (bar.includes(item.path)) return;
      expect(reachable.has(item.path)).withContext(`${item.path} có trong "Thêm"`).toBeTrue();
      expect(reachable.get(item.path)).withContext(item.path).toContain(item.label);
    });
  });

  it('"Thêm" shows nothing that is not in NAV_GROUPS (no second list), and does not repeat the four bar pages', () => {
    openMore();

    const known = new Set(allNavItems().map(i => i.path));
    const bar = barPaths();
    expect(bar.length).toBe(4);
    moreLinks().forEach(a => {
      expect(known.has(a.getAttribute('href')!)).withContext(a.getAttribute('href') ?? '').toBeTrue();
      expect(bar).not.toContain(a.getAttribute('href'));
    });
  });

  it('the pages on the bar are themselves in NAV_GROUPS', () => {
    const known = new Set(allNavItems().map(i => i.path));

    barPaths().forEach(p => expect(known.has(p!)).withContext(p ?? '').toBeTrue());
  });

  it('"Thêm" is grouped under the NAV_GROUPS headings', () => {
    openMore();

    const text = el().textContent ?? '';
    NAV_GROUPS.forEach(g => expect(text).withContext(g.key).toContain(g.label));
  });
});
