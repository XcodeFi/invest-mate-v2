import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CompanyDossierDetailComponent } from './company-dossier-detail.component';

describe('CompanyDossierDetailComponent: thanh nút đầu trang trên màn hẹp', () => {
  let fixture: ComponentFixture<CompanyDossierDetailComponent>;

  const render = (width: number) => {
    fixture = TestBed.createComponent(CompanyDossierDetailComponent);
    const host = fixture.nativeElement as HTMLElement;
    host.style.display = 'block';
    host.style.width = `${width}px`;
    host.style.overflowX = 'auto';
    document.body.appendChild(host);
    fixture.componentInstance.symbol = 'FPT';
    fixture.detectChanges();
    fixture.componentInstance.loading = false;
    fixture.componentInstance.mode = 'view';
    fixture.detectChanges();
    return host;
  };

  const ids = ['btn-copy', 'btn-open-share', 'btn-open-paste', 'btn-edit'];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyDossierDetailComponent, HttpClientTestingModule],
      providers: [provideRouter([])],
    }).compileComponents();
  });
  afterEach(() => fixture?.destroy());

  it('toolbar_buttons_stay_inside_the_viewport_at_400px', () => {
    const host = render(400);
    const hostRight = host.getBoundingClientRect().right;

    for (const id of ids) {
      const btn = host.querySelector(`[data-testid="${id}"]`) as HTMLElement;
      expect(btn).withContext(id).not.toBeNull();
      expect(btn.getBoundingClientRect().right).withContext(id).toBeLessThanOrEqual(hostRight + 0.5);
    }
    expect(host.scrollWidth).withContext('không cuộn ngang').toBeLessThanOrEqual(host.clientWidth);
  });

  it('toolbar_stays_on_one_row_on_wide_screens', () => {
    const host = render(1100);
    const centres = ids.map(id => {
      const r = (host.querySelector(`[data-testid="${id}"]`) as HTMLElement).getBoundingClientRect();
      return r.top + r.height / 2;
    });

    expect(Math.max(...centres) - Math.min(...centres)).toBeLessThan(2);   // cùng một hàng
  });
});
