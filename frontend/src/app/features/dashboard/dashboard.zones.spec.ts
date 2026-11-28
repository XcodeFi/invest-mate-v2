import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NEVER, of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../../core/services/auth.service';
import { PnlService } from '../../core/services/pnl.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { AdvancedAnalyticsService } from '../../core/services/advanced-analytics.service';
import { MarketDataService } from '../../core/services/market-data.service';
import { PositionsService } from '../../core/services/positions.service';
import { NotificationService } from '../../core/services/notification.service';
import { DailyRoutineService } from '../../core/services/daily-routine.service';
import { WatchlistService } from '../../core/services/watchlist.service';
import { CapitalFlowService } from '../../core/services/capital-flow.service';
import { MoodService } from '../../core/services/mood.service';
import { PersonalFinanceService } from '../../core/services/personal-finance.service';
import { PatienceHeroComponent } from './widgets/patience-hero.component';
import { DecisionQueueComponent } from './widgets/decision-queue.component';
import { DisciplineScoreWidgetComponent } from './widgets/discipline-score-widget.component';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';

@Component({ selector: 'app-patience-hero', standalone: true, template: '<div data-test="stub-patience"></div>' })
class PatienceStub {}
@Component({ selector: 'app-decision-queue', standalone: true, template: '<div data-test="stub-queue"></div>' })
class QueueStub {}
@Component({ selector: 'app-discipline-score-widget', standalone: true, template: '<div data-test="stub-discipline"></div>' })
class DisciplineStub {}
@Component({ selector: 'app-ai-chat-panel', standalone: true, template: '' })
class AiStub {
  @Input() isOpen = false;
  @Output() isOpenChange = new EventEmitter<boolean>();
  @Input() title = '';
  @Input() useCase = '';
  @Input() contextData: unknown;
}

const SUMMARY = {
  hasProfile: true, totalAssets: 535_000_000, securitiesValue: 350_000_000, goldTotal: 0, savingsTotal: 100_000_000, emergencyTotal: 30_000_000,
  idleCashTotal: 5_000_000, totalDebt: 0, netWorth: 535_000_000, hasHighInterestConsumerDebt: false, monthlyExpense: 15_000_000,
  healthScore: 85, ruleChecks: [], accounts: [], debts: [],
};

describe('DashboardComponent — các vùng Hôm nay / Tài sản / Theo dõi', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;

  beforeEach(() => {
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['getCurrentUser']);
    auth.getCurrentUser.and.returnValue(of({ name: 'An' } as any));
    const pnl = jasmine.createSpyObj<PnlService>('PnlService', ['getSummary']);
    pnl.getSummary.and.returnValue(of({ totalInvested: 0, totalPortfolioValue: 100_000_000, portfolios: [] } as any));
    const portfolios = jasmine.createSpyObj<PortfolioService>('PortfolioService', ['getAll']);
    portfolios.getAll.and.returnValue(of([]));
    const analytics = jasmine.createSpyObj<AdvancedAnalyticsService>('AdvancedAnalyticsService', ['getEquityCurve', 'getHouseholdPerformance']);
    analytics.getEquityCurve.and.returnValue(NEVER);
    analytics.getHouseholdPerformance.and.returnValue(NEVER);
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getBatchPrices']);
    market.getBatchPrices.and.returnValue(of([]));
    const positions = jasmine.createSpyObj<PositionsService>('PositionsService', ['getAll']);
    positions.getAll.and.returnValue(of([]));
    const routine = jasmine.createSpyObj<DailyRoutineService>('DailyRoutineService', ['getToday', 'getSuggestedTemplate', 'getOrCreateToday', 'completeItem']);
    routine.getToday.and.returnValue(of(null as any));
    routine.getSuggestedTemplate.and.returnValue(of(null as any));
    const watchlist = jasmine.createSpyObj<WatchlistService>('WatchlistService', ['getAll', 'getDetail']);
    watchlist.getAll.and.returnValue(of([]));
    const flows = jasmine.createSpyObj<CapitalFlowService>('CapitalFlowService', ['getFlowHistory', 'getTimeWeightedReturn']);
    flows.getFlowHistory.and.returnValue(of(null as any));
    flows.getTimeWeightedReturn.and.returnValue(of(null as any));
    const mood = jasmine.createSpyObj<MoodService>('MoodService', ['markOverride']);
    const finance = jasmine.createSpyObj<PersonalFinanceService>('PersonalFinanceService', ['getSummary']);
    finance.getSummary.and.returnValue(of(SUMMARY as any));

    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth }, { provide: PnlService, useValue: pnl }, { provide: PortfolioService, useValue: portfolios },
        { provide: AdvancedAnalyticsService, useValue: analytics }, { provide: MarketDataService, useValue: market },
        { provide: PositionsService, useValue: positions }, { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['error']) },
        { provide: DailyRoutineService, useValue: routine }, { provide: WatchlistService, useValue: watchlist },
        { provide: CapitalFlowService, useValue: flows }, { provide: MoodService, useValue: mood }, { provide: PersonalFinanceService, useValue: finance },
      ],
    });
    TestBed.overrideComponent(DashboardComponent, {
      remove: { imports: [PatienceHeroComponent, DecisionQueueComponent, DisciplineScoreWidgetComponent, AiChatPanelComponent] },
      add: { imports: [PatienceStub, QueueStub, DisciplineStub, AiStub] },
    });
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    component.moodResolved = true;
    component.cagrValue = 12.3;
    component.cagrTarget = 15;
    fixture.detectChanges();
  });

  afterEach(() => fixture?.destroy());

  const root = () => fixture.nativeElement as HTMLElement;
  const q = (id: string) => root().querySelector(`[data-test="${id}"]`) as HTMLElement | null;

  it('the zones appear in the order Hôm nay, Tài sản, Theo dõi', () => {
    const order = Array.from(root().querySelectorAll('section[data-test^="zone-"]')).map(e => e.getAttribute('data-test'));

    expect(order).toEqual(['zone-today', 'zone-assets', 'zone-watch']);
    expect(Array.from(root().querySelectorAll('[data-test^="zone-"][data-test$="-title"]')).map(e => e.textContent!.trim())).toEqual(['Hôm nay', 'Tài sản', 'Theo dõi']);
  });

  it('Hôm nay holds the patience hero, the decision queue and the discipline row, in that order', () => {
    const today = q('zone-today')!;
    const kids = ['stub-patience', 'stub-queue', 'stub-discipline'].map(id => today.querySelector(`[data-test="${id}"]`));

    kids.forEach(k => expect(k).not.toBeNull());
    expect(kids[0]!.compareDocumentPosition(kids[1]!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(kids[1]!.compareDocumentPosition(kids[2]!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('Tài sản holds the investment hero and the finance widget side by side from 1280px, stacked below', () => {
    const assets = q('zone-assets')!;
    const row = assets.querySelector('[data-test="assets-row"]') as HTMLElement;

    expect(row.className).toContain('grid-cols-1');
    expect(row.className).toContain('min-[1280px]:grid-cols-2');
    expect(row.contains(q('investment-hero'))).toBeTrue();
    expect(row.querySelector('app-networth-summary')).not.toBeNull();
  });

  it('the timeframe switcher sits inside the investment hero', () => {
    const hero = q('investment-hero')!;

    expect(hero.contains(q('timeframe-switcher'))).toBeTrue();
    expect(root().querySelectorAll('[data-test="timeframe-switcher"]').length).toBe(1);
  });

  it('there is no second "+ Tạo Danh mục mới" in the header (the portfolios card keeps its own)', () => {
    const header = root().querySelector('.shadow-sm.border-b') as HTMLElement;

    expect(header.textContent).not.toContain('Tạo Danh mục mới');
    expect(header.querySelector('[data-test="ai-button"]')).not.toBeNull();
    expect(q('zone-watch')!.textContent).toContain('Tạo');   // thẻ Danh mục vẫn có nút tạo của riêng nó
  });

  it('the CAGR number is rendered exactly once across the whole dashboard, collapsed AND expanded', () => {
    const count = () => (root().textContent!.replace(/\s+/g, ' ').match(/12\.3%/g) ?? []).length;

    expect(count()).toBe(1);   // thu gọn: chỉ hero
    expect(q('investment-hero')!.textContent).toContain('CAGR +12.3%');
    expect(q('compound-summary')!.textContent).not.toContain('12.3');

    (q('compound-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(count()).toBe(1);   // mở: vẫn chỉ hero
    expect(q('compound-body')!.textContent).not.toContain('12.3%');
  });

  it('the collapsed summary states the target and its horizon exactly', () => {
    component.cagrTarget = 15;
    component.targetYears = 10;
    fixture.detectChanges();

    expect(q('compound-summary')!.textContent!.replace(/\s+/g, ' ').trim())
      .toBe('Mục tiêu 15%/năm trong 10 năm. Số CAGR hiện tại nằm ở khối Tổng quan đầu tư phía trên.');
  });

  it('target editing still works inside the expanded card: Đặt mục tiêu opens the editor and a second click closes it', () => {
    (q('compound-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();
    const editorBtn = () => Array.from(q('compound-body')!.querySelectorAll('button')).find(b => /mục tiêu|Đóng/.test(b.textContent!) && /^(Đặt mục tiêu|Đóng)$/.test(b.textContent!.trim())) as HTMLButtonElement;
    expect(editorBtn().textContent!.trim()).toBe('Đặt mục tiêu');
    expect(q('compound-body')!.textContent).not.toContain('CAGR mục tiêu (%/năm)');

    editorBtn().click();
    fixture.detectChanges();
    expect(q('compound-body')!.textContent).toContain('CAGR mục tiêu (%/năm)');
    expect(editorBtn().textContent!.trim()).toBe('Đóng');

    editorBtn().click();
    fixture.detectChanges();
    expect(q('compound-body')!.textContent).not.toContain('CAGR mục tiêu (%/năm)');
  });

  it('the Lãi kép toggle has a hit area of at least 32px (padding + min-height)', () => {
    const toggle = q('compound-toggle') as HTMLButtonElement;

    expect(toggle.classList).toContain('min-h-[32px]');
    expect(toggle.classList).toContain('py-2');
  });

  it('the Lãi kép card is collapsed by default with a one-line summary, and its toggle opens and closes the body', () => {
    expect(q('compound-body')).toBeNull();
    expect(q('compound-summary')).not.toBeNull();
    const toggle = () => q('compound-toggle') as HTMLButtonElement;
    expect(toggle().getAttribute('aria-expanded')).toBe('false');
    expect(toggle().textContent!.trim()).toBe('Xem chi tiết ▾');

    toggle().click();
    fixture.detectChanges();
    expect(q('compound-body')).not.toBeNull();
    expect(q('compound-summary')).toBeNull();
    expect(toggle().getAttribute('aria-expanded')).toBe('true');
    expect(toggle().textContent!.trim()).toBe('Thu gọn ▴');

    toggle().click();
    fixture.detectChanges();
    expect(q('compound-body')).toBeNull();
  });

  it('the projections and the target editor are still there once expanded (only the duplicate CAGR number was removed)', () => {
    (q('compound-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    const body = q('compound-body')!;
    expect(body.textContent).toContain('Ước tính giá trị vốn');
    expect(body.textContent).toContain('Mục tiêu');
    expect(body.textContent).toContain('Đặt mục tiêu');
  });

  it('Theo dõi holds the portfolios and the top positions (the watchlist/routine cards depend on data)', () => {
    const text = q('zone-watch')!.textContent!;

    ['Danh mục của bạn', 'Vị thế nổi bật'].forEach(t => expect(text).toContain(t));
  });
});
