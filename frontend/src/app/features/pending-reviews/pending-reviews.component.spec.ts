import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { PendingReviewsComponent } from './pending-reviews.component';
import { DisciplineService } from '../../core/services/discipline.service';
import { CompanyDossierService, DossierReviewItemDto } from '../../core/services/company-dossier.service';

/**
 * Mục "Hồ sơ công ty cần soát lại". Cổng hồ sơ chỉ bắn lúc lập kế hoạch, nên đây là đường duy nhất
 * để biết trước — và nó phải nói rõ trạng thái nào đang CHẶN, không chỉ liệt kê.
 */
describe('PendingReviewsComponent — hồ sơ cần soát lại', () => {
  let disciplineSpy: jasmine.SpyObj<DisciplineService>;
  let dossierSpy: jasmine.SpyObj<CompanyDossierService>;

  const item = (over: Partial<DossierReviewItemDto> = {}): DossierReviewItemDto => ({
    symbol: 'HPG',
    freshness: 'Expired',
    reviewedAt: '2026-01-01T00:00:00Z',
    daysOverdue: 110,
    ...over,
  } as DossierReviewItemDto);

  beforeEach(() => {
    disciplineSpy = jasmine.createSpyObj('DisciplineService', ['getPendingReviews']);
    disciplineSpy.getPendingReviews.and.returnValue(of([]));
    dossierSpy = jasmine.createSpyObj('CompanyDossierService', ['needingReview']);
    dossierSpy.needingReview.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [PendingReviewsComponent],
      providers: [
        { provide: DisciplineService, useValue: disciplineSpy },
        { provide: CompanyDossierService, useValue: dossierSpy },
        provideRouter([]),
      ],
    });
  });

  it('không có hồ sơ nào cần soát thì ẩn hẳn mục, không hiện "0 hồ sơ"', () => {
    const fixture = TestBed.createComponent(PendingReviewsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Hồ sơ công ty cần soát lại');
  });

  it('render mã, nhãn trạng thái và số ngày quá hạn', () => {
    dossierSpy.needingReview.and.returnValue(of([item()]));
    const fixture = TestBed.createComponent(PendingReviewsComponent);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Hồ sơ công ty cần soát lại');
    expect(text).toContain('HPG');
    expect(text).toContain('quá hạn 110 ngày');
  });

  it('nói rõ Expired và Unconfirmed đang chặn lập kế hoạch, NeedsReview thì không', () => {
    // Đây là điểm khác biệt duy nhất giữa "phải làm ngay" và "nên làm": thiếu nó thì ba trạng thái
    // trông như nhau và người dùng không biết cái nào đang khoá mình.
    const fixture = TestBed.createComponent(PendingReviewsComponent);
    const c = fixture.componentInstance;

    expect(c.dossierBlocks('Expired')).toBeTrue();
    expect(c.dossierBlocks('Unconfirmed')).toBeTrue();
    expect(c.dossierBlocks('NeedsReview')).toBeFalse();
  });

  it('hồ sơ chưa ký không hiện số ngày quá hạn', () => {
    // daysOverdue = 0 vì đồng hồ hạn tươi chưa chạy; hiện "quá hạn 0 ngày" là bịa một con số.
    dossierSpy.needingReview.and.returnValue(of([
      item({ symbol: 'VNM', freshness: 'Unconfirmed', daysOverdue: 0 } as Partial<DossierReviewItemDto>)
    ]));
    const fixture = TestBed.createComponent(PendingReviewsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('VNM');
    expect(fixture.nativeElement.textContent).not.toContain('quá hạn');
  });

  it('lỗi tải hồ sơ không làm trắng danh sách plan cần review', () => {
    // Hai danh sách độc lập: một cái chết không được kéo cái kia đi theo.
    dossierSpy.needingReview.and.returnValue(throwError(() => new Error('boom')));
    const fixture = TestBed.createComponent(PendingReviewsComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.error).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Không có plan nào cần review');
  });
});
