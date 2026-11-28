import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SidebarComponent } from './sidebar.component';
import { SIDEBAR_STORAGE_KEY } from './sidebar.service';
import { AuthService } from '../../../core/services/auth.service';
import { allNavItems, NAV_GROUPS } from '../../nav/nav-config';

@Component({ standalone: true, template: '' })
class BlankComponent {}

describe('SidebarComponent: menu trái nhóm theo thời điểm làm việc', () => {
  let fixture: ComponentFixture<SidebarComponent>;
  let router: Router;
  let authenticated = true;

  const settingsItems = NAV_GROUPS.find(g => g.collapsedByDefault)!.items;

  function setup() {
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([{ path: '**', component: BlankComponent }]),
        { provide: AuthService, useValue: { isAuthenticated: () => authenticated } },
      ],
    });
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
  }

  async function go(url: string) {
    await router.navigateByUrl(url);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const q = (id: string) => el().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const qa = (id: string) => Array.from(el().querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
  const link = (path: string) => qa('sidebar-link').find(a => a.getAttribute('href') === path);
  const isRail = () => q('sidebar')!.getAttribute('data-collapsed') === 'true';

  beforeEach(() => { authenticated = true; try { localStorage.removeItem(SIDEBAR_STORAGE_KEY); } catch { /* bỏ qua */ } });
  afterEach(() => { fixture?.destroy(); try { localStorage.removeItem(SIDEBAR_STORAGE_KEY); } catch { /* bỏ qua */ } });

  describe('nội dung', () => {
    it('renders the groups in order and every page of the open groups from the config', () => {
      setup();

      expect(qa('sidebar-group-label').map(g => (g.textContent ?? '').trim())).toEqual(NAV_GROUPS.slice(0, -1).map(g => g.label));
      const open = NAV_GROUPS.filter(g => !g.collapsedByDefault).flatMap(g => g.items.map(i => i.path));
      expect(qa('sidebar-link').map(a => a.getAttribute('href'))).toEqual(open);
    });

    it('includes the pages that have no entry in the old header menu', () => {
      setup();

      ['/pending-reviews', '/campaign-analytics', '/corporate-actions', '/screener', '/symbol-timeline']
        .forEach(path => expect(link(path)).withContext(path).toBeDefined());
    });

    it('the Cài đặt group is collapsed by default and opens on click', () => {
      setup();
      expect(link(settingsItems[0].path)).toBeUndefined();

      q('sidebar-settings-toggle')!.click();
      fixture.detectChanges();

      settingsItems.forEach(i => expect(link(i.path)).withContext(i.path).toBeDefined());
      q('sidebar-settings-toggle')!.click();
      fixture.detectChanges();
      expect(link(settingsItems[0].path)).toBeUndefined();
    });

    it('a manual open/close of the Cài đặt group applies to the current page only and resets after navigating', async () => {
      setup();
      await go('/dashboard');
      q('sidebar-settings-toggle')!.click();   // mở tay trên trang ngoài nhóm
      fixture.detectChanges();
      expect(link(settingsItems[0].path)).toBeDefined();

      await go('/alerts');
      expect(link(settingsItems[0].path)).withContext('đổi trang thì mở tay phải được bỏ').toBeUndefined();

      await go('/api-keys');
      expect(link('/api-keys')).toBeDefined();   // tự mở vì trang nằm trong nhóm
      q('sidebar-settings-toggle')!.click();      // đóng tay
      fixture.detectChanges();
      expect(link('/api-keys')).toBeUndefined();

      await go('/ai-settings');
      expect(link('/ai-settings')).withContext('sang trang khác trong nhóm thì tự mở lại').toBeDefined();
    });

    it('opens the Cài đặt group by itself when the current page is inside it', async () => {
      setup();

      await go('/api-keys');

      expect(link('/api-keys')).toBeDefined();
    });
  });

  describe('trang đang mở được tô sáng', () => {
    it('marks only the link of the current route as the current page', async () => {
      setup();

      await go('/positions');

      const current = qa('sidebar-link').filter(a => a.getAttribute('aria-current') === 'page');
      expect(current.map(a => a.getAttribute('href'))).toEqual(['/positions']);
      expect(link('/positions')!.className).toContain('bg-blue-50');
      expect(link('/trades')!.className).not.toContain('bg-blue-50');
    });

    it('keeps the highlight when the page has params and a query string', async () => {
      setup();

      await go('/symbol-timeline/FPT?range=6T');

      expect(link('/symbol-timeline')!.getAttribute('aria-current')).toBe('page');
    });

    it('moves the highlight when the route changes', async () => {
      setup();
      await go('/dashboard');
      await go('/alerts');

      expect(link('/dashboard')!.getAttribute('aria-current')).toBeNull();
      expect(link('/alerts')!.getAttribute('aria-current')).toBe('page');
    });
  });

  describe('thanh biểu tượng (rail)', () => {
    it('is expanded by default on ordinary pages', async () => {
      setup();

      await go('/dashboard');

      expect(isRail()).toBeFalse();
      expect(q('sidebar')!.className).toContain('w-56');
      expect((link('/dashboard')!.textContent ?? '')).toContain('Tổng quan');
    });

    for (const url of ['/symbol-timeline/FPT', '/symbol-timeline', '/trade-plan', '/market-data', '/trade-replay/abc']) {
      it(`defaults to the rail on the chart route ${url}`, async () => {
        setup();

        await go(url);

        expect(isRail()).toBeTrue();
        expect(q('sidebar')!.className).toContain('w-14');
      });
    }

    it('the rail shows icons only, with the page name as tooltip and accessible name', async () => {
      setup();
      await go('/market-data');

      const a = link('/positions')!;
      expect((a.textContent ?? '').trim()).toBe('📌');
      expect(a.getAttribute('title')).toBe('Vị thế');
      expect(a.getAttribute('aria-label')).toBe('Vị thế');
      expect(qa('sidebar-group-label').length).toBe(0);
    });

    it('expanded links keep the name visible and need no tooltip', async () => {
      setup();
      await go('/dashboard');

      const a = link('/positions')!;
      expect(a.getAttribute('title')).toBeNull();
      expect(a.getAttribute('aria-label')).toBe('Vị thế');
    });

    it('the toggle reads "« Thu gọn" when expanded and a clear name when collapsed', async () => {
      setup();
      await go('/dashboard');
      expect(q('sidebar-toggle')!.textContent).toContain('« Thu gọn');

      q('sidebar-toggle')!.click();
      fixture.detectChanges();

      expect(isRail()).toBeTrue();
      expect(q('sidebar-toggle')!.getAttribute('aria-label')).toBe('Mở rộng menu');
    });
  });

  describe('lựa chọn của người dùng được lưu và luôn thắng mặc định', () => {
    it('clicking the toggle saves the choice', async () => {
      setup();
      await go('/dashboard');

      q('sidebar-toggle')!.click();
      fixture.detectChanges();

      expect(localStorage.getItem(SIDEBAR_STORAGE_KEY)).toBe('1');
      q('sidebar-toggle')!.click();
      fixture.detectChanges();
      expect(localStorage.getItem(SIDEBAR_STORAGE_KEY)).toBe('0');
    });

    it('the first toggle click on a chart route with no saved choice expands the menu and saves "expanded"', async () => {
      setup();
      await go('/symbol-timeline/FPT');
      expect(isRail()).toBeTrue();   // mặc định theo trang, chưa có lựa chọn nào được lưu
      expect(localStorage.getItem(SIDEBAR_STORAGE_KEY)).toBeNull();

      q('sidebar-toggle')!.click();
      fixture.detectChanges();

      expect(isRail()).toBeFalse();
      expect(localStorage.getItem(SIDEBAR_STORAGE_KEY)).toBe('0');
    });

    it('a saved "expanded" wins over the chart-route default', async () => {
      localStorage.setItem(SIDEBAR_STORAGE_KEY, '0');
      setup();

      await go('/symbol-timeline/FPT');

      expect(isRail()).toBeFalse();
    });

    it('a saved "collapsed" wins on ordinary pages', async () => {
      localStorage.setItem(SIDEBAR_STORAGE_KEY, '1');
      setup();

      await go('/dashboard');

      expect(isRail()).toBeTrue();
    });

    it('the choice survives a reload (a new component reads it back)', async () => {
      setup();
      await go('/dashboard');
      q('sidebar-toggle')!.click();
      fixture.detectChanges();
      fixture.destroy();
      TestBed.resetTestingModule();

      setup();
      await go('/dashboard');

      expect(isRail()).toBeTrue();
    });

    it('without a saved choice the default follows the route; with one it does not', async () => {
      setup();
      await go('/dashboard');
      expect(isRail()).toBeFalse();
      await go('/trade-plan');
      expect(isRail()).toBeTrue();
      await go('/positions');
      expect(isRail()).toBeFalse();

      q('sidebar-toggle')!.click();   // chọn thu gọn trên trang thường
      fixture.detectChanges();
      await go('/trades');
      expect(isRail()).toBeTrue();
      await go('/dashboard');
      expect(isRail()).toBeTrue();
    });

    it('an invalid saved value is ignored (falls back to the route default)', async () => {
      localStorage.setItem(SIDEBAR_STORAGE_KEY, 'maybe');
      setup();

      await go('/dashboard');
      expect(isRail()).toBeFalse();
      await go('/market-data');
      expect(isRail()).toBeTrue();
    });

    it('blocked storage does not break the menu: the default applies and the toggle still works for the session', async () => {
      const original = Object.getOwnPropertyDescriptor(window, 'localStorage')!;
      Object.defineProperty(window, 'localStorage', { configurable: true, get: () => { throw new Error('bị chặn'); } });
      try {
        setup();
        await go('/dashboard');
        expect(isRail()).toBeFalse();

        expect(() => { q('sidebar-toggle')!.click(); fixture.detectChanges(); }).not.toThrow();
        expect(isRail()).toBeTrue();
      } finally {
        Object.defineProperty(window, 'localStorage', original);
      }
    });
  });

  describe('hiển thị theo màn hình và đăng nhập', () => {
    it('is hidden below 1024px (hidden, shown only from the lg breakpoint) and sticks under the top bar', () => {
      setup();

      const cls = q('sidebar')!.classList;
      expect(cls.contains('hidden')).toBeTrue();
      expect(cls.contains('lg:flex')).toBeTrue();
      expect(cls.contains('sticky')).toBeTrue();
    });

    it('the expand/collapse button stays visible without scrolling (sticky at the bottom of the menu)', async () => {
      setup();
      await go('/market-data');   // thanh biểu tượng: nút « / » phải thấy ngay cả khi danh sách trang dài hơn màn hình

      const cls = q('sidebar-toggle')!.classList;
      expect(cls.contains('sticky')).toBeTrue();
      expect(cls.contains('bottom-0')).toBeTrue();
      expect(q('sidebar-toggle')!.parentElement).toBe(q('sidebar'));   // nằm trực tiếp trong khung cuộn để dính đáy
    });

    it('is not rendered when nobody is signed in', () => {
      authenticated = false;
      setup();

      expect(q('sidebar')).toBeNull();
    });

    it('lists every nav item once across the open and the opened-on-demand groups', () => {
      setup();
      q('sidebar-settings-toggle')!.click();
      fixture.detectChanges();

      expect(qa('sidebar-link').map(a => a.getAttribute('href'))).toEqual(allNavItems().map(i => i.path));
    });
  });
});
