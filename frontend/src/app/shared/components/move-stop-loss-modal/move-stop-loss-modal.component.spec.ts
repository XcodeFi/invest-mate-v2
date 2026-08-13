import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MoveStopLossModalComponent } from './move-stop-loss-modal.component';

/**
 * Gate lý do khi nới SL nằm trong modal dùng chung, nên cả ba mặt gọi nó (danh sách kế hoạch,
 * Decision Queue, trang Quản lý rủi ro) cùng chịu một luật. Xem ADR-0017.
 */
describe('MoveStopLossModalComponent', () => {
  let fixture: ComponentFixture<MoveStopLossModalComponent>;
  let component: MoveStopLossModalComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MoveStopLossModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(MoveStopLossModalComponent);
    component = fixture.componentInstance;
    component.open = true;
    component.symbol = 'MWG';
    component.direction = 'Buy';
    component.currentStopLoss = 64700;
    fixture.detectChanges();
  });

  it('coi Buy dời xuống là nới SL', () => {
    component.newStopLoss = 60000;

    expect(component.isWidening).toBe(true);
  });

  it('coi Buy dời lên là siết SL', () => {
    component.newStopLoss = 71000;

    expect(component.isWidening).toBe(false);
  });

  it('coi Sell dời lên là nới SL', () => {
    component.direction = 'Sell';
    component.newStopLoss = 70000;

    expect(component.isWidening).toBe(true);
  });

  it('chặn nới SL khi lý do rỗng', () => {
    const emitted: unknown[] = [];
    component.confirm.subscribe(v => emitted.push(v));
    component.newStopLoss = 60000;
    component.reason = '   ';

    component.submit();

    expect(emitted.length).toBe(0);
    expect(component.error).toContain('lý do');
  });

  it('cho nới SL khi có lý do, phát ra đúng giá và lý do', () => {
    const emitted: { newStopLoss: number; reason?: string }[] = [];
    component.confirm.subscribe(v => emitted.push(v));
    component.newStopLoss = 60000;
    component.reason = 'Chốt 50% rồi, cho phần còn lại biên rộng hơn';

    component.submit();

    expect(emitted).toEqual([{
      newStopLoss: 60000,
      reason: 'Chốt 50% rồi, cho phần còn lại biên rộng hơn'
    }]);
    expect(component.error).toBeNull();
  });

  it('cho siết SL không cần lý do', () => {
    const emitted: { newStopLoss: number; reason?: string }[] = [];
    component.confirm.subscribe(v => emitted.push(v));
    component.newStopLoss = 71000;
    component.reason = '';

    component.submit();

    expect(emitted).toEqual([{ newStopLoss: 71000, reason: undefined }]);
  });

  it('chặn khi SL mới không phải số dương', () => {
    const emitted: unknown[] = [];
    component.confirm.subscribe(v => emitted.push(v));
    component.newStopLoss = 0;

    component.submit();

    expect(emitted.length).toBe(0);
    expect(component.error).toBeTruthy();
  });

  it('chặn khi SL mới bằng SL hiện tại', () => {
    const emitted: unknown[] = [];
    component.confirm.subscribe(v => emitted.push(v));
    component.newStopLoss = 64700;

    component.submit();

    expect(emitted.length).toBe(0);
    expect(component.error).toBeTruthy();
  });

  it('hiện cảnh báo điểm kỷ luật khi đang nới', () => {
    component.newStopLoss = 60000;
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('kỷ luật');
  });

  it('không hiện cảnh báo khi đang siết', () => {
    component.newStopLoss = 71000;
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('điểm kỷ luật');
  });

  it('xoá lỗi cũ khi mở lại', () => {
    component.newStopLoss = 0;
    component.submit();
    expect(component.error).toBeTruthy();

    component.open = false;
    component.ngOnChanges();
    component.open = true;
    component.ngOnChanges();

    expect(component.error).toBeNull();
    expect(component.reason).toBe('');
  });
});
