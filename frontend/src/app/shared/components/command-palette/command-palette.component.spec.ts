import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { CommandPaletteComponent } from './command-palette.component';
import { CommandPaletteService } from './command-palette.service';
import { buildItems, normalize } from './command-palette.logic';
import { AuthService } from '../../../core/services/auth.service';
import { allNavItems } from '../../nav/nav-config';

@Component({ standalone: true, template: '' })
class BlankComponent {}

describe('CommandPaletteComponent: Ctrl+K tìm trang và mã', () => {
  let fixture: ComponentFixture<CommandPaletteComponent>;
  let router: Router;
  let svc: CommandPaletteService;
  let authenticated = true;

  function setup() {
    TestBed.configureTestingModule({
      imports: [CommandPaletteComponent],
      providers: [
        provideRouter([{ path: '**', component: BlankComponent }]),
        { provide: AuthService, useValue: { isAuthenticated: () => authenticated } },
      ],
    });
    router = TestBed.inject(Router);
    svc = TestBed.inject(CommandPaletteService);
    fixture = TestBed.createComponent(CommandPaletteComponent);
    fixture.detectChanges();
  }
  const el = () => fixture.nativeElement as HTMLElement;
  const q = (id: string) => el().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const qa = (id: string) => Array.from(el().querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
  const input = () => q('palette-input') as HTMLInputElement;
  function key(k: string, init: KeyboardEventInit = {}) {
    const e = new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true, ...init });
    document.dispatchEvent(e);
    fixture.detectChanges();
    return e;
  }
  async function type(v: string) {
    await fixture.whenStable(); // ngModel ghi giá trị đầu ở microtask: chờ xong rồi mới gõ như người dùng thật
    fixture.detectChanges();
    input().value = v;
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }
  function open() { svc.open(); fixture.detectChanges(); }

  beforeEach(() => { authenticated = true; });
  afterEach(() => fixture?.destroy());

  it('đóng sẵn: chưa có hộp thoại nào', () => {
    setup();
    expect(q('palette')).toBeNull();
  });

  it('Ctrl+K mở bảng lệnh, Ctrl+K lần nữa đóng lại, và chặn phím tắt mặc định của trình duyệt', () => {
    setup();

    const e = key('k', { ctrlKey: true });
    expect(q('palette')).not.toBeNull();
    expect(e.defaultPrevented).toBeTrue();

    key('k', { ctrlKey: true });
    expect(q('palette')).toBeNull();
  });

  it('Cmd+K (macOS) cũng mở; chữ K viết hoa vẫn nhận', () => {
    setup();
    key('K', { metaKey: true });
    expect(q('palette')).not.toBeNull();
  });

  it('chữ k trơn không mở (đang gõ ở ô khác)', () => {
    setup();
    key('k');
    expect(q('palette')).toBeNull();
  });

  it('chưa đăng nhập thì Ctrl+K không mở gì', () => {
    authenticated = false;
    setup();
    key('k', { ctrlKey: true });
    expect(q('palette')).toBeNull();
  });

  it('Esc đóng bảng lệnh', () => {
    setup();
    open();

    key('Escape');

    expect(q('palette')).toBeNull();
  });

  it('bấm nền mờ đóng, bấm trong hộp không đóng', () => {
    setup();
    open();

    q('palette-box')!.click(); fixture.detectChanges();
    expect(q('palette')).not.toBeNull();

    q('palette-backdrop')!.click(); fixture.detectChanges();
    expect(q('palette')).toBeNull();
  });

  it('ô nhập dùng appUppercase: gõ chữ thường thì hiển thị chữ hoa', async () => {
    setup();
    open();

    await type('fpt');

    expect(input().value).toBe('FPT');
  });

  it('để trống thì liệt kê mọi trang của menu (cùng cấu hình), không có dòng mã', () => {
    setup();
    open();

    expect(qa('palette-item').filter(i => i.dataset['kind'] === 'page').map(i => i.dataset['path'])).toEqual(allNavItems().map(i => i.path));
    expect(qa('palette-item').some(i => i.dataset['kind'] === 'symbol')).toBeFalse();
  });

  it('gõ không dấu vẫn tìm ra trang có dấu', async () => {
    setup();
    open();

    await type('ke hoach');

    expect(qa('palette-item').map(i => i.dataset['path'])).toContain('/trade-plan');
  });

  it('gõ một mã thì có dòng "Mở mã" trỏ tới dòng thời gian của mã', async () => {
    setup();
    open();

    await type('fpt');

    const s = qa('palette-item').find(i => i.dataset['kind'] === 'symbol')!;
    expect(s.dataset['path']).toBe('/symbol-timeline/FPT');
    expect(s.textContent).toContain('FPT');
  });

  it('Enter đi tới mục đang chọn và đóng bảng lệnh', async () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();
    await type('vcb');

    key('Enter');

    expect(nav).toHaveBeenCalledWith('/symbol-timeline/VCB');
    expect(q('palette')).toBeNull();
  });

  it('mũi tên xuống rồi Enter đi tới mục thứ hai; mũi tên lên ở đầu danh sách không vượt biên', () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();

    key('ArrowUp');
    key('ArrowDown');
    key('Enter');

    expect(nav).toHaveBeenCalledWith(allNavItems()[1].path);
  });

  it('mũi tên xuống ở cuối danh sách không vượt biên: Enter vẫn mở mục cuối', async () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();
    await type('thi truong');   // thu hẹp danh sách còn vài mục
    const n = qa('palette-item').length;
    const lastPath = qa('palette-item')[qa('palette-item').length - 1].dataset['path']!;

    for (let i = 0; i < n + 5; i++) key('ArrowDown');
    key('Enter');

    expect(nav).toHaveBeenCalledTimes(1);
    expect(nav).toHaveBeenCalledWith(lastPath);
  });

  it('không có gì khớp thì báo "Không tìm thấy" và Enter không đi đâu', async () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();

    await type('zz zz');
    key('Enter');

    expect(q('palette-empty')!.textContent).toContain('Không tìm thấy');
    expect(nav).not.toHaveBeenCalled();
    expect(q('palette')).not.toBeNull();
  });

  it('mở lại thì ô nhập trống', async () => {
    setup();
    open();
    await type('fpt');
    key('Escape');

    open();

    expect(input().value).toBe('');
  });

  it('hộp thoại có role dialog và aria-modal', () => {
    setup();
    open();
    expect(q('palette-box')!.getAttribute('role')).toBe('dialog');
    expect(q('palette-box')!.getAttribute('aria-modal')).toBe('true');
  });
});

describe('command-palette.logic', () => {
  it('normalize bỏ dấu và đ', () => {
    expect(normalize('Kế hoạch GD, Đầu tư')).toBe('ke hoach gd, dau tu');
  });

  it('mã hợp lệ: 2-10 ký tự chữ/số bắt đầu bằng chữ; chuỗi có dấu cách hoặc quá ngắn thì không', () => {
    const sym = (s: string) => buildItems(s).some(i => i.kind === 'symbol');
    expect(sym('FPT')).toBeTrue();
    expect(sym('e1vfvn30')).toBeTrue();
    expect(sym('F')).toBeFalse();
    expect(sym('FPT HPG')).toBeFalse();
    expect(sym('123')).toBeFalse();
    expect(sym('ABCDEFGHIJK')).toBeFalse();
  });

  it('trang khớp đứng trước dòng mã', () => {
    const items = buildItems('ai');
    expect(items.length).toBeGreaterThan(1);
    expect(items[0].kind).toBe('page');
    expect(items[items.length - 1].kind).toBe('symbol');
  });
});
