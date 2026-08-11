import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SymbolLinkDirective } from './symbol-link.directive';

@Component({
  standalone: true,
  imports: [SymbolLinkDirective],
  template: `<span [appSymbolLink]="sym">{{ sym }}</span>`,
})
class HostComponent {
  sym: string | null | undefined = 'hpg';
}

describe('SymbolLinkDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  const span = (): HTMLElement => fixture.nativeElement.querySelector('span');

  it('điều hướng tới dòng thời gian của mã, chuẩn hoá về chữ in', () => {
    const spy = spyOn(router, 'navigate');

    span().click();

    expect(spy).toHaveBeenCalledWith(['/symbol-timeline', 'HPG']);
  });

  it('bấm Enter cũng đi được — không chỉ chuột', () => {
    const spy = spyOn(router, 'navigate');

    span().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(spy).toHaveBeenCalledWith(['/symbol-timeline', 'HPG']);
  });

  it('mã rỗng thì không giả vờ là link và không điều hướng', () => {
    fixture.componentInstance.sym = '   ';
    fixture.detectChanges();
    const spy = spyOn(router, 'navigate');

    span().click();

    expect(spy).not.toHaveBeenCalled();
    expect(span().getAttribute('role')).toBeNull();
    expect(span().getAttribute('tabindex')).toBeNull();
  });

  it('mã null/undefined không làm vỡ template', () => {
    fixture.componentInstance.sym = null;
    fixture.detectChanges();
    const spy = spyOn(router, 'navigate');

    span().click();

    expect(spy).not.toHaveBeenCalled();
  });

  it('mã rỗng thì click vẫn nổi lên cha', () => {
    // Nếu directive stopPropagation vô điều kiện thì hàng bảng có (click) riêng
    // sẽ chết theo ở mọi chỗ ta gắn directive — hỏng nhiều hơn là sửa.
    fixture.componentInstance.sym = '';
    fixture.detectChanges();
    let bubbled = false;
    fixture.nativeElement.addEventListener('click', () => (bubbled = true));

    span().click();

    expect(bubbled).toBeTrue();
  });

  it('mã có giá trị thì CHẶN click khỏi nổi lên cha', () => {
    // Hàng bảng thường có (click) mở modal chi tiết. Bấm vào mã phải đi timeline,
    // không được vừa đi timeline vừa mở modal.
    let bubbled = false;
    fixture.nativeElement.addEventListener('click', () => (bubbled = true));
    spyOn(router, 'navigate');

    span().click();

    expect(bubbled).toBeFalse();
  });

  it('CHẶN click của hàng — lý do không được gắn vào hàng có (click) riêng', () => {
    // Đây không phải tính năng để ăn mừng, mà là ràng buộc phải nhớ: directive
    // nuốt click của phần tử cha. Gắn nó vào một hàng mà cú bấm hàng LÀ hành
    // động chính (nạp kế hoạch, chọn mã, tra cứu) là cướp mất hành động đó —
    // build và test component đều không thấy, chỉ bấm thật mới thấy.
    let rowAction = 0;
    fixture.nativeElement.addEventListener('click', () => rowAction++);
    spyOn(router, 'navigate');

    span().click();

    expect(rowAction).toBe(0);
  });

  it('gắn nhãn trợ năng khi có mã', () => {
    expect(span().getAttribute('role')).toBe('link');
    expect(span().getAttribute('tabindex')).toBe('0');
    expect(span().getAttribute('title')).toBe('Xem dòng thời gian HPG');
  });
});
