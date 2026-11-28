import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BottomNavComponent } from './bottom-nav.component';

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

  it('the market page stays next to it', () => {
    openMore();

    expect(moreLinks().some(a => a.getAttribute('href') === '/market-data')).toBeTrue();
  });

  it('every "Thêm" link goes to a different page', () => {
    openMore();

    const hrefs = moreLinks().map(a => a.getAttribute('href'));
    expect(new Set(hrefs).size).toBe(hrefs.length);
  });
});
