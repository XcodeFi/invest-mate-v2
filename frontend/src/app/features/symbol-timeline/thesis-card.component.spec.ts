import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ThesisCardComponent } from './thesis-card.component';
import { CompanyDossierDto, CompanyDossierService } from '../../core/services/company-dossier.service';

const daysAgoIso = (n: number) => new Date(Date.now() - n * 86_400_000).toISOString();

const dossier = (over: Partial<CompanyDossierDto> = {}): CompanyDossierDto => ({
  symbol: 'HPG',
  businessModel: 'Bán thép xây dựng và HRC cho nhà thầu nội địa',
  moats: [{ description: 'Chi phí đơn vị thấp nhất ngành' }],
  riskFactors: [
    { rank: 1, description: 'Giá HRC Trung Quốc', observableSignal: 'Giá HRC giảm quá 10% trong một tháng', isDealBreaker: true, suggestedTrigger: null },
    { rank: 2, description: 'Biên lợi nhuận', observableSignal: 'Biên gộp dưới 8% hai quý liền', isDealBreaker: false, suggestedTrigger: null },
    { rank: 3, description: 'Nợ vay', observableSignal: 'Nợ/vốn chủ vượt 1,2', isDealBreaker: false, suggestedTrigger: null },
    { rank: 4, description: 'Thuế chống bán phá giá', observableSignal: 'Bộ Công Thương mở điều tra', isDealBreaker: true, suggestedTrigger: null },
  ],
  notes: null, reviewedAt: daysAgoIso(12), confirmedAt: daysAgoIso(12), agentDraftedAt: null, freshness: 'Fresh',
  ...over,
} as CompanyDossierDto);

describe('ThesisCardComponent (thẻ Luận điểm)', () => {
  let fixture: ComponentFixture<ThesisCardComponent>;
  let service: jasmine.SpyObj<CompanyDossierService>;

  function setup(result: 'ok' | '404' | '500' = 'ok', dto: CompanyDossierDto = dossier()) {
    service = jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']);
    service.get.and.callFake(() => result === 'ok' ? of(dto)
      : throwError(() => new HttpErrorResponse({ status: result === '404' ? 404 : 500 })));
    TestBed.configureTestingModule({
      imports: [ThesisCardComponent],
      providers: [provideRouter([]), { provide: CompanyDossierService, useValue: service }],
    });
    fixture = TestBed.createComponent(ThesisCardComponent);
    fixture.componentRef.setInput('symbol', 'HPG');
    fixture.detectChanges();
  }

  const q = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const toggle = () => { (q('thesis-toggle') as HTMLButtonElement).click(); fixture.detectChanges(); };

  afterEach(() => fixture?.destroy());

  it('is collapsed by default and makes no request', () => {
    setup();

    expect(q('thesis-body')).toBeNull();
    expect(q('thesis-toggle')!.getAttribute('aria-expanded')).toBe('false');
    expect(service.get).not.toHaveBeenCalled();
  });

  it('requests the dossier only once expanded, and only once even when toggled again', () => {
    setup();

    toggle();
    expect(service.get).toHaveBeenCalledOnceWith('HPG');
    toggle();   // thu lại
    toggle();   // mở lại
    expect(service.get).toHaveBeenCalledTimes(1);
  });

  it('shows the business model, the top 3 risks, the invalidation conditions, the signed date and age', () => {
    setup();

    toggle();

    expect(q('thesis-model')!.textContent).toContain('Bán thép xây dựng và HRC');
    const risks = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="thesis-risk"]')) as HTMLElement[];
    expect(risks.length).toBe(3);
    expect(risks[0].textContent).toContain('Giá HRC Trung Quốc');
    const conditions = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="thesis-invalidation"]')) as HTMLElement[];
    expect(conditions.map(c => c.textContent!.trim())).toEqual(['Giá HRC giảm quá 10% trong một tháng', 'Bộ Công Thương mở điều tra']);
    expect(q('thesis-signed')!.textContent).toContain('12 ngày trước');
  });

  it('links to the full dossier', () => {
    setup();
    toggle();

    expect(q('thesis-open-dossier')!.getAttribute('href')).toBe('/company-dossier/HPG');
  });

  it('a 404 shows "Chưa có hồ sơ" and the create-dossier link', () => {
    setup('404');

    toggle();

    expect(q('thesis-empty')!.textContent).toContain('Chưa có hồ sơ');
    expect(q('thesis-create-dossier')!.getAttribute('href')).toBe('/company-dossier/HPG');
    expect(q('thesis-error')).toBeNull();
  });

  it('another failure shows an error and can be retried by collapsing and expanding again', () => {
    setup('500');

    toggle();
    expect(q('thesis-error')).not.toBeNull();
    expect(q('thesis-empty')).toBeNull();
    toggle();
    toggle();

    expect(service.get).toHaveBeenCalledTimes(2);
  });

  it('an unsigned dossier says so instead of showing a signed age', () => {
    setup('ok', dossier({ confirmedAt: null, freshness: 'Unconfirmed' }));

    toggle();

    expect(q('thesis-signed')!.textContent).toContain('Chưa ký');
  });

  it('a dossier with no deal-breaker risk says no invalidation condition was written', () => {
    setup('ok', dossier({ riskFactors: [{ rank: 1, description: 'Rủi ro', observableSignal: 'Dấu hiệu', isDealBreaker: false, suggestedTrigger: null }] }));

    toggle();

    expect(q('thesis-no-invalidation')).not.toBeNull();
  });

  it('the create-plan link carries the symbol to the trade-plan form (?symbol=)', () => {
    setup();

    const link = q('thesis-create-plan') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/trade-plan?symbol=HPG');
  });
});
