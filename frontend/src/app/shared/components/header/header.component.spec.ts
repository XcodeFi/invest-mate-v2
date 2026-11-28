import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { BehaviorSubject, of } from 'rxjs';
import { CommandPaletteService } from '../command-palette/command-palette.service';
import { HeaderComponent } from './header.component';
import { AuthService } from '../../../core/services/auth.service';
import { PnlService } from '../../../core/services/pnl.service';
import { RiskService } from '../../../core/services/risk.service';
import { allNavItems, NAV_GROUPS } from '../../nav/nav-config';

describe('HeaderComponent: thanh trên gọn, menu điện thoại đọc từ cấu hình chung', () => {
  let fixture: ComponentFixture<HeaderComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated: () => true, getToken: () => null, currentUser$: new BehaviorSubject(null), logout: () => of(null) } },
        { provide: PnlService, useValue: { getSummary: () => of({ portfolios: [] }) } },
        { provide: RiskService, useValue: {} },
      ],
    });
    fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
  });
  afterEach(() => fixture?.destroy());

  const el = () => fixture.nativeElement as HTMLElement;
  const q = (id: string) => el().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const openMobile = () => {
    (el().querySelector('button.lg\\:hidden') as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  it('no longer carries the desktop dropdown menu (the sidebar replaced it)', () => {
    expect(q('desktop-nav')).toBeNull();
    const text = el().textContent ?? '';
    ['Đầu tư', 'Phân tích', 'Quản lý'].forEach(old => expect(text).withContext(old).not.toContain(old));
  });

  it('renders the theme toggle in the slot of the top bar', () => {
    const slot = q('topbar-theme-slot')!;

    expect(slot.querySelector('app-theme-toggle')).not.toBeNull();
    expect(slot.querySelector('[data-testid="theme-toggle"]')).not.toBeNull();
  });

  it('has a search button that opens the command palette (phone users have no Ctrl+K)', () => {
    const svc = TestBed.inject(CommandPaletteService);
    expect(svc.isOpen()).toBeFalse();

    (q('topbar-search') as HTMLButtonElement).click();

    expect(svc.isOpen()).toBeTrue();
  });

  it('keeps the logo that links to the dashboard', () => {
    const logo = el().querySelector('a[href="/dashboard"]') as HTMLAnchorElement;
    expect(logo.textContent).toContain('Investment Mate');
  });

  it('leaves a slot for the theme toggle in the top bar', () => {
    expect(q('topbar-theme-slot')).not.toBeNull();
  });

  it('the mobile menu lists the same groups as the config, in the same order', () => {
    openMobile();

    const labels = Array.from(el().querySelectorAll('[data-testid="mobile-group"]')).map(g => (g.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(labels.length).toBe(NAV_GROUPS.length);
    NAV_GROUPS.forEach((g, i) => expect(labels[i]).toContain(g.label));
  });

  it('opening a mobile group shows its pages from the config, including pages the old menu never listed', () => {
    openMobile();
    const reviewGroup = Array.from(el().querySelectorAll('[data-testid="mobile-group"]'))
      .find(g => (g.textContent ?? '').includes('ĐÁNH GIÁ')) as HTMLButtonElement;

    reviewGroup.click();
    fixture.detectChanges();

    const hrefs = Array.from(el().querySelectorAll('[data-testid="mobile-link"]')).map(a => a.getAttribute('href'));
    const expected = NAV_GROUPS.find(g => g.label === 'ĐÁNH GIÁ')!.items.map(i => i.path);
    expect(hrefs).toEqual(expected);
    expect(hrefs).toContain('/pending-reviews');
    expect(hrefs).toContain('/campaign-analytics');
  });

  it('clicking an already open mobile group closes it (accordion toggles, it does not only open)', () => {
    openMobile();
    const group = el().querySelector('[data-testid="mobile-group"]') as HTMLButtonElement;

    group.click();
    fixture.detectChanges();
    expect(el().querySelectorAll('[data-testid="mobile-link"]').length).toBeGreaterThan(0);

    group.click();
    fixture.detectChanges();
    expect(el().querySelectorAll('[data-testid="mobile-link"]').length).toBe(0);
  });

  it('opening another mobile group closes the previous one', () => {
    openMobile();
    const groups = Array.from(el().querySelectorAll('[data-testid="mobile-group"]')) as HTMLButtonElement[];

    groups[0].click();
    fixture.detectChanges();
    groups[1].click();
    fixture.detectChanges();

    const hrefs = Array.from(el().querySelectorAll('[data-testid="mobile-link"]')).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(NAV_GROUPS[1].items.map(i => i.path));
  });

  it('every page of the config is reachable from the mobile menu', () => {
    openMobile();
    const seen: string[] = [];
    Array.from(el().querySelectorAll('[data-testid="mobile-group"]')).forEach(g => {
      (g as HTMLButtonElement).click();
      fixture.detectChanges();
      el().querySelectorAll('[data-testid="mobile-link"]').forEach(a => seen.push(a.getAttribute('href')!));
    });

    allNavItems().forEach(i => expect(seen).withContext(i.path).toContain(i.path));
  });
});
