import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DossierViewComponent } from './dossier-view.component';
import { RiskFactorDto } from '../../core/services/company-dossier.service';

function risk(over: Partial<RiskFactorDto> = {}): RiskFactorDto {
  return {
    rank: 1,
    description: 'Nợ xấu tăng',
    observableSignal: 'NPL vượt 3% hai quý liên tiếp',
    isDealBreaker: false,
    suggestedTrigger: null,
    ...over,
  };
}

describe('DossierViewComponent', () => {
  let fixture: ComponentFixture<DossierViewComponent>;
  let component: DossierViewComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DossierViewComponent] }).compileComponents();
    fixture = TestBed.createComponent(DossierViewComponent);
    component = fixture.componentInstance;
  });

  function html(): string {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).innerHTML;
  }

  it('gắn badge "Yếu tố hủy diệt" đúng vào yếu tố được đánh dấu', () => {
    component.riskFactors = [risk({ isDealBreaker: true })];
    const el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    expect(el.querySelectorAll('[data-testid="deal-breaker-badge"]').length).toBe(1);
  });

  it('không có yếu tố hủy diệt thì không có badge nào', () => {
    component.riskFactors = [risk(), risk({ rank: 2 })];
    const el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    expect(el.querySelectorAll('[data-testid="deal-breaker-badge"]').length).toBe(0);
  });

  it('giữ nguyên thứ tự hạng do component cha truyền xuống', () => {
    component.riskFactors = [
      risk({ rank: 1, description: 'Rủi ro một' }),
      risk({ rank: 2, description: 'Rủi ro hai' }),
      risk({ rank: 3, description: 'Rủi ro ba' }),
    ];
    const text = html().replace(/<[^>]+>/g, ' ');

    expect(text.indexOf('Rủi ro một')).toBeLessThan(text.indexOf('Rủi ro hai'));
    expect(text.indexOf('Rủi ro hai')).toBeLessThan(text.indexOf('Rủi ro ba'));
  });

  it('hiện dấu hiệu quan sát được — đó là dòng phải soi khi cầm mã', () => {
    component.riskFactors = [risk({ observableSignal: 'Biên lãi gộp giảm 2 quý' })];
    expect(html()).toContain('Biên lãi gộp giảm 2 quý');
  });

  it('ghi chú rỗng thì bỏ hẳn khối, không để tiêu đề trơ trọi', () => {
    component.notes = '   ';
    const el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="view-notes"]')).toBeNull();
  });

  it('ghi chú có nội dung thì hiện', () => {
    component.notes = 'Theo dõi room ngoại';
    const el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="view-notes"]')).not.toBeNull();
  });

  it('không có kịch bản vô hiệu hoá thì không render chip', () => {
    component.riskFactors = [risk({ suggestedTrigger: null })];
    const el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="trigger-chip"]')).toBeNull();
  });

  it('kịch bản vô hiệu hoá hiện bằng nhãn tiếng Việt, không phải mã enum', () => {
    component.riskFactors = [risk({ suggestedTrigger: 'EarningsMiss' })];
    const el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="trigger-chip"]')!.textContent!.trim()).toBe('KQKD không đạt');
  });

  it('trigger lạ (backend thêm giá trị mới) hiện nguyên mã thay vì biến mất', () => {
    expect(component.triggerLabel('SomethingNew')).toBe('SomethingNew');
  });

  it('bỏ qua moat rỗng — chip trắng không nói gì cả', () => {
    component.moats = [{ description: 'Thương hiệu' }, { description: '  ' }];
    expect(component.visibleMoats().length).toBe(1);
  });
});
