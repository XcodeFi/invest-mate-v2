import { ComponentFixture, TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from '../../../core/services/theme.service';
import { ThemeToggleComponent } from './theme-toggle.component';

describe('ThemeToggleComponent', () => {
  let fixture: ComponentFixture<ThemeToggleComponent>;
  const button = () => fixture.nativeElement.querySelector('[data-testid="theme-toggle"]') as HTMLButtonElement;
  const icon = () => (fixture.nativeElement.querySelector('[data-testid="theme-toggle-icon"]') as HTMLElement).textContent!.trim();

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ThemeToggleComponent] });
    TestBed.inject(ThemeService).set('dark', false);
    fixture = TestBed.createComponent(ThemeToggleComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    TestBed.inject(ThemeService).set('dark', false);   // trả về mặc định cho các spec khác
    try { localStorage.removeItem(THEME_STORAGE_KEY); } catch { /* bỏ qua */ }
  });

  it('shows the sun and the Vietnamese tooltip "Chế độ sáng" while dark', () => {
    expect(icon()).toBe('☀️');
    expect(button().title).toBe('Chế độ sáng');
    expect(button().getAttribute('aria-label')).toBe('Chế độ sáng');
  });

  it('click flips to light: moon icon, tooltip "Chế độ tối", class removed from <html>, choice saved', () => {
    button().click();
    fixture.detectChanges();

    expect(icon()).toBe('🌙');
    expect(button().title).toBe('Chế độ tối');
    expect(document.documentElement.classList.contains('dark')).toBeFalse();
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');
  });

  it('a second click returns to dark', () => {
    button().click();
    fixture.detectChanges();
    button().click();
    fixture.detectChanges();

    expect(icon()).toBe('☀️');
    expect(document.documentElement.classList.contains('dark')).toBeTrue();
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });
});
