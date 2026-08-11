import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { CompanyDossierListComponent } from './company-dossier-list.component';
import { CompanyDossierService, CompanyDossierDto } from '../../core/services/company-dossier.service';

const dossier = (symbol: string): CompanyDossierDto => ({
  symbol,
  businessModel: 'Mô hình kinh doanh đủ dài để không vướng cổng',
  moats: [],
  riskFactors: [],
  notes: null,
  reviewedAt: new Date().toISOString(),
  confirmedAt: new Date().toISOString(),
  agentDraftedAt: null,
  freshness: 'Fresh',
  version: 1,
} as unknown as CompanyDossierDto);

describe('CompanyDossierListComponent — appSymbolLink', () => {
  let fixture: ComponentFixture<CompanyDossierListComponent>;
  let serviceSpy: jasmine.SpyObj<CompanyDossierService>;

  function setup(items: CompanyDossierDto[]) {
    serviceSpy = jasmine.createSpyObj('CompanyDossierService', ['list']);
    serviceSpy.list.and.returnValue(of(items));

    TestBed.configureTestingModule({
      imports: [CompanyDossierListComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CompanyDossierService, useValue: serviceSpy },
      ],
    });
    fixture = TestBed.createComponent(CompanyDossierListComponent);
  }

  it('mỗi mã trong bảng bấm được sang dòng thời gian', () => {
    // Mẫu đại diện cho BẢNG. Quên đưa SymbolLinkDirective vào `imports` thì
    // Angular bỏ qua attribute trong im lặng — mã trông y hệt, chỉ là không
    // bấm được. Chỉ DOM bắt được ca đó.
    setup([dossier('HPG'), dossier('FPT'), dossier('SSI')]);
    fixture.detectChanges();

    const links = fixture.debugElement.queryAll(By.css('[role="link"][title^="Xem dòng thời gian"]'));

    expect(links.length).toBe(3);
    expect(links.map(l => l.nativeElement.getAttribute('title')))
      .toEqual(['Xem dòng thời gian HPG', 'Xem dòng thời gian FPT', 'Xem dòng thời gian SSI']);
  });

  it('bảng rỗng thì không có link mã nào', () => {
    setup([]);
    fixture.detectChanges();

    expect(fixture.debugElement.queryAll(By.css('[role="link"][title^="Xem dòng thời gian"]')).length).toBe(0);
  });
});
