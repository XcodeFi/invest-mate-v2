import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IndicatorGroupBarComponent } from './indicator-group-bar.component';
import { GROUP_STARS_KEY } from '../../indicators/ta-group-stars';

describe('IndicatorGroupBarComponent: sao nhóm', () => {
  let fixture: ComponentFixture<IndicatorGroupBarComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const chips = () => Array.from(el().querySelectorAll('[data-group]')).map(b => b.getAttribute('data-group'));
  const star = (g: string) => el().querySelector(`[data-star="${g}"]`) as HTMLButtonElement;

  function create(stored?: string[]) {
    localStorage.removeItem(GROUP_STARS_KEY);
    if (stored) localStorage.setItem(GROUP_STARS_KEY, JSON.stringify(stored));
    TestBed.configureTestingModule({ imports: [IndicatorGroupBarComponent] });
    fixture = TestBed.createComponent(IndicatorGroupBarComponent);
    fixture.detectChanges();
  }
  afterEach(() => { fixture?.destroy(); localStorage.removeItem(GROUP_STARS_KEY); });

  it('each chip has its own star button with an aria-label that names the group', () => {
    create();

    expect(el().querySelectorAll('[data-star]').length).toBe(6);
    expect(star('xu-huong').textContent!.trim()).toBe('☆');
    expect(star('xu-huong').getAttribute('aria-label')).toBe('Sao nhóm 📈 Xu hướng');
    expect(star('xu-huong').getAttribute('aria-pressed')).toBe('false');
  });

  it('the star is an on/off toggle: aria-pressed carries the state, the label stays the same, and the idle star is not the low-contrast amber-500', () => {
    create();

    expect(star('xu-huong').className).not.toContain('text-amber-500');
    star('xu-huong').click();
    fixture.detectChanges();
    expect(star('xu-huong').getAttribute('aria-pressed')).toBe('true');
    expect(star('xu-huong').getAttribute('aria-label')).toBe('Sao nhóm 📈 Xu hướng');
  });

  it('starring toggles ★, writes localStorage, and does not emit a chip click', () => {
    create();
    const emitted: string[] = [];
    fixture.componentInstance.groupToggle.subscribe(g => emitted.push(g));

    star('moc-gia').click();
    fixture.detectChanges();

    expect(emitted).toEqual([]);
    expect(star('moc-gia').textContent!.trim()).toBe('★');
    expect(star('moc-gia').getAttribute('aria-label')).toBe('Sao nhóm 🧱 Mốc giá');
    expect(JSON.parse(localStorage.getItem(GROUP_STARS_KEY)!)).toEqual(['moc-gia']);

    star('moc-gia').click();
    fixture.detectChanges();
    expect(star('moc-gia').textContent!.trim()).toBe('☆');
    expect(JSON.parse(localStorage.getItem(GROUP_STARS_KEY)!)).toEqual([]);
  });

  it('clicking the chip still emits the group and does not touch the stars', () => {
    create();
    const emitted: string[] = [];
    fixture.componentInstance.groupToggle.subscribe(g => emitted.push(g));

    (el().querySelector('[data-group="dong-tien"]') as HTMLButtonElement).click();

    expect(emitted).toEqual(['dong-tien']);
    expect(localStorage.getItem(GROUP_STARS_KEY)).toBeNull();
  });

  it('starred chips come first, TA_GROUPS order inside each part', () => {
    create(['moc-gia', 'dong-luong']);

    expect(chips()).toEqual(['dong-luong', 'moc-gia', 'tong-hop', 'xu-huong', 'bien-dong', 'dong-tien']);
  });

  it('unknown stored keys are ignored', () => {
    create(['khong-co', 'xu-huong']);

    expect(chips()[0]).toBe('xu-huong');
    expect(chips().length).toBe(6);
  });
});
