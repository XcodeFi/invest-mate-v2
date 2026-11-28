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

  it('Ctrl+Shift+K không mở (trình duyệt dùng tổ hợp này)', () => {
    setup();
    key('k', { ctrlKey: true, shiftKey: true });
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

  it('gõ tên nhóm menu (không có trong nhãn trang nào) vẫn ra các trang của nhóm đó', async () => {
    setup();
    open();

    await type('tim co hoi');

    const paths = qa('palette-item').map(i => i.dataset['path']);
    expect(paths).toContain('/screener');
    expect(paths).toContain('/watchlist');
  });

  it('gõ chữ mới thì chọn lại mục đầu: ↓↓ rồi gõ rồi Enter mở mục đầu của danh sách mới', async () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();
    await fixture.whenStable();
    key('ArrowDown'); key('ArrowDown');

    await type('ke hoach');
    const first = qa('palette-item')[0].dataset['path']!;
    key('Enter');

    expect(nav).toHaveBeenCalledWith(first);
  });

  it('đang gõ tiếng Việt có dấu (IME, isComposing) thì Enter và mũi tên không chọn mục', async () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();

    key('ArrowDown', { isComposing: true });
    key('Enter', { isComposing: true });

    expect(nav).not.toHaveBeenCalled();
    expect(q('palette')).not.toBeNull();
    key('Enter');   // hết gõ dở: Enter chọn mục (mục đầu, vì ↓ lúc gõ dở không có tác dụng)
    expect(nav).toHaveBeenCalledWith(allNavItems()[0].path);
  });

  it('mã gõ vào đứng ĐẦU danh sách: Enter mở mã dù chuỗi cũng khớp tên trang', async () => {
    setup();
    const nav = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    open();

    await type('ai');   // vừa là mã vừa khớp "Cài đặt AI"
    expect(qa('palette-item')[0].dataset['kind']).toBe('symbol');
    key('Enter');

    expect(nav).toHaveBeenCalledWith('/symbol-timeline/AI');
  });

  it('Tab khi đang mở không rời khỏi hộp thoại (bẫy tiêu điểm); khi đóng thì Tab bình thường', () => {
    setup();
    expect(key('Tab').defaultPrevented).toBeFalse();

    open();
    expect(key('Tab').defaultPrevented).toBeTrue();
    expect(key('Tab', { shiftKey: true }).defaultPrevented).toBeTrue();
  });

  it('đóng thì trả tiêu điểm về phần tử đã mở bảng lệnh', async () => {
    setup();
    const opener = document.createElement('button');
    document.body.appendChild(opener);
    try {
      opener.focus();
      svc.open(); fixture.detectChanges();
      await new Promise(r => setTimeout(r));
      expect(document.activeElement).withContext('ô nhập đã nhận tiêu điểm').toBe(input());

      key('Escape');

      expect(document.activeElement).toBe(opener);
    } finally { opener.remove(); }
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

  it('ô nhập cỡ 16px để iOS Safari không phóng to trang khi bấm vào', () => {
    setup();
    open();

    expect(parseFloat(getComputedStyle(input()).fontSize)).toBeGreaterThanOrEqual(16);
  });

  it('nhãn nhóm ở dòng đang chọn đạt tương phản AA (≥ 4,5:1) ở chế độ sáng', () => {
    const wasDark = document.documentElement.classList.contains('dark');
    document.documentElement.classList.remove('dark');   // chế độ sáng, bất kể spec khác để lại gì
    setup();
    open();
    const lum = (rgb: string) => {
      const [r, g, b] = (rgb.match(/[\d.]+/g) ?? ['0', '0', '0']).slice(0, 3).map(Number).map(v => { const c = v / 255; return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); });
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    const row = qa('palette-item')[0];
    const hint = row.querySelector('span:last-child') as HTMLElement;
    const fill = lum(getComputedStyle(row).backgroundColor);
    const ink = lum(getComputedStyle(hint).color);

    if (wasDark) document.documentElement.classList.add('dark');
    expect((Math.max(fill, ink) + 0.05) / (Math.min(fill, ink) + 0.05)).toBeGreaterThanOrEqual(4.5);
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

  it('dòng mã đứng đầu, các trang khớp theo sau', () => {
    const items = buildItems('ai');
    expect(items.length).toBeGreaterThan(1);
    expect(items[0].kind).toBe('symbol');
    expect(items.slice(1).every(i => i.kind === 'page')).toBeTrue();
  });
});
