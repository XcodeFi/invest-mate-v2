import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';

import { PersonalFinanceComponent } from './personal-finance.component';
import {
  PersonalFinanceService,
  FinancialAccountDto,
  FinancialAccountType,
  UpsertFinancialAccountRequest,
} from '../../core/services/personal-finance.service';
import { NotificationService } from '../../core/services/notification.service';

describe('PersonalFinanceComponent — Savings term dates', () => {
  let component: PersonalFinanceComponent;
  let fixture: ComponentFixture<PersonalFinanceComponent>;
  let financeSpy: jasmine.SpyObj<PersonalFinanceService>;

  beforeEach(async () => {
    financeSpy = jasmine.createSpyObj('PersonalFinanceService', [
      'getSummary', 'getProfile', 'upsertProfile', 'upsertAccount', 'removeAccount',
      'upsertDebt', 'removeDebt', 'getGoldPrices',
    ]);
    const notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);

    financeSpy.getSummary.and.returnValue(of({
      hasProfile: true, totalAssets: 0, securitiesValue: 0, goldTotal: 0, savingsTotal: 0,
      emergencyTotal: 0, idleCashTotal: 0, totalDebt: 0, netWorth: 0, hasHighInterestConsumerDebt: false,
      monthlyExpense: 0, healthScore: 0, ruleChecks: [], accounts: [], debts: [],
    } as any));
    financeSpy.getProfile.and.returnValue(of(null as any));
    financeSpy.upsertAccount.and.returnValue(of({} as any));

    await TestBed.configureTestingModule({
      imports: [PersonalFinanceComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PersonalFinanceService, useValue: financeSpy },
        { provide: NotificationService, useValue: notifySpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PersonalFinanceComponent);
    component = fixture.componentInstance;
  });

  it('setSavingsTermMonths(12) auto-tính maturity = deposit + 12 tháng', () => {
    component.formSavingsDepositDate = '2026-01-15';
    component.setSavingsTermMonths(12);
    expect(component.formSavingsMaturityDate).toBe('2027-01-15');
  });

  it('setSavingsTermMonths(6) no-op khi formSavingsDepositDate chưa nhập', () => {
    component.formSavingsDepositDate = null;
    component.formSavingsMaturityDate = null;
    component.setSavingsTermMonths(6);
    expect(component.formSavingsMaturityDate).toBeNull();
  });

  it('onTypeChange: chuyển từ Savings sang IdleCash nulls interest + 2 dates (fix leak)', () => {
    component.formType = FinancialAccountType.Savings;
    component.formInterestRate = 6.5;
    component.formSavingsDepositDate = '2026-01-15';
    component.formSavingsMaturityDate = '2027-01-15';

    component.formType = FinancialAccountType.IdleCash;
    component.onTypeChange();

    expect(component.formInterestRate).toBeNull();
    expect(component.formSavingsDepositDate).toBeNull();
    expect(component.formSavingsMaturityDate).toBeNull();
  });

  it('submitAccountForm: gửi dates khi Type === Savings', () => {
    component.formName = 'Tiết kiệm VCB';
    component.formType = FinancialAccountType.Savings;
    component.formBalance = 100_000_000;
    component.formInterestRate = 5.5;
    component.formSavingsDepositDate = '2026-01-15';
    component.formSavingsMaturityDate = '2027-01-15';

    component.submitAccountForm();

    expect(financeSpy.upsertAccount).toHaveBeenCalled();
    const req: UpsertFinancialAccountRequest = financeSpy.upsertAccount.calls.mostRecent().args[0];
    expect(req.depositDate).toBe('2026-01-15');
    expect(req.maturityDate).toBe('2027-01-15');
    expect(req.interestRate).toBe(5.5);
  });

  it('submitAccountForm: KHÔNG gửi dates khi Type !== Savings', () => {
    component.formName = 'Tiền mặt';
    component.formType = FinancialAccountType.IdleCash;
    component.formBalance = 5_000_000;
    // Giả sử state rác còn sót từ lần edit Savings trước — submit không được gửi lên
    component.formSavingsDepositDate = '2026-01-15';
    component.formSavingsMaturityDate = '2027-01-15';

    component.submitAccountForm();

    const req: UpsertFinancialAccountRequest = financeSpy.upsertAccount.calls.mostRecent().args[0];
    expect(req.depositDate).toBeUndefined();
    expect(req.maturityDate).toBeUndefined();
    expect(req.interestRate).toBeUndefined();
  });

  it('openEditAccountForm: prefill null-safe khi account không có dates', () => {
    const account: FinancialAccountDto = {
      id: 'a1',
      type: FinancialAccountType.Savings,
      name: 'Sổ không kỳ hạn',
      balance: 50_000_000,
      interestRate: 3,
      depositDate: null,
      maturityDate: null,
      updatedAt: '2026-04-23T00:00:00Z',
    };
    component.openEditAccountForm(account);
    expect(component.formSavingsDepositDate).toBeNull();
    expect(component.formSavingsMaturityDate).toBeNull();
  });

  it('openEditAccountForm: prefill dates từ ISO string (chỉ lấy phần date)', () => {
    const account: FinancialAccountDto = {
      id: 'a1',
      type: FinancialAccountType.Savings,
      name: 'Sổ 12T',
      balance: 100_000_000,
      interestRate: 5.5,
      depositDate: '2026-01-15T00:00:00Z',
      maturityDate: '2027-01-15T00:00:00Z',
      updatedAt: '2026-04-23T00:00:00Z',
    };
    component.openEditAccountForm(account);
    expect(component.formSavingsDepositDate).toBe('2026-01-15');
    expect(component.formSavingsMaturityDate).toBe('2027-01-15');
  });
});
