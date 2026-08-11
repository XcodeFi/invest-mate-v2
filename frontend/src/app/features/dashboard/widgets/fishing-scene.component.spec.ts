import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FishingSceneComponent } from './fishing-scene.component';

describe('FishingSceneComponent', () => {
  let fixture: ComponentFixture<FishingSceneComponent>;
  let component: FishingSceneComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FishingSceneComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FishingSceneComponent);
    component = fixture.componentInstance;
  });

  describe('kẹp giá trị calm', () => {
    it('giữ nguyên giá trị trong khoảng 0..1', () => {
      component.calm = 0.4;
      expect(component.calm).toBe(0.4);
    });

    it('kẹp giá trị lớn hơn 1 về 1', () => {
      component.calm = 7;
      expect(component.calm).toBe(1);
    });

    it('kẹp giá trị âm về 0', () => {
      component.calm = -3;
      expect(component.calm).toBe(0);
    });

    it('coi NaN là 0 thay vì để màu thành rgb(NaN,...)', () => {
      component.calm = Number.NaN;
      expect(component.calm).toBe(0);
    });
  });

  describe('biến CSS theo mức tĩnh', () => {
    it('hồ càng lặng thì biên độ càng nhỏ và sóng càng chậm', () => {
      component.calm = 0;
      const choppy = component.cssVars;
      component.calm = 1;
      const glassy = component.cssVars;

      expect(parseFloat(glassy['--amp'])).toBeLessThan(parseFloat(choppy['--amp']));
      expect(parseFloat(glassy['--dur'])).toBeGreaterThan(parseFloat(choppy['--dur']));
      expect(parseFloat(glassy['--drift'])).toBeGreaterThan(parseFloat(choppy['--drift']));
    });

    it('trả cùng một object khi calm không đổi — không cấp phát mỗi vòng dò thay đổi', () => {
      component.calm = 0.5;

      expect(component.cssVars).toBe(component.cssVars);
    });

    it('dựng lại object khi calm đổi', () => {
      component.calm = 0.2;
      const before = component.cssVars;
      component.calm = 0.8;

      expect(component.cssVars).not.toBe(before);
    });

    it('hồ càng lặng thì BIÊN ĐỘ đường sóng càng nhỏ — không chỉ chậm lại', () => {
      const ampOf = (d: string) => Math.abs(parseFloat(d.split('q 50 ')[1]));

      component.calm = 0;
      const choppy = component.waves.map(w => ampOf(w.d));
      component.calm = 1;
      const glassy = component.waves.map(w => ampOf(w.d));

      glassy.forEach((amp, i) => expect(amp).toBeLessThan(choppy[i]));
    });

    it('mặt hồ phẳng vẫn còn một chút gợn, không chết cứng thành đường thẳng', () => {
      component.calm = 1;

      component.waves.forEach(w => expect(Math.abs(parseFloat(w.d.split('q 50 ')[1]))).toBeGreaterThan(0));
    });

    it('không sinh ra NaN trong biến CSS ở mọi mức', () => {
      [0, 0.25, 0.5, 0.75, 1].forEach(value => {
        component.calm = value;
        Object.values(component.cssVars).forEach(v => expect(v).not.toContain('NaN'));
      });
    });
  });

  describe('dim khi có cảm xúc', () => {
    it('làm tối trời nhưng KHÔNG đổi biên độ hay tốc độ sóng', () => {
      component.calm = 0.5;
      const brightSky = component.skyTop;
      const waveVars = component.cssVars;

      component.dim = true;

      expect(component.skyTop).not.toBe(brightSky);
      expect(component.cssVars).toEqual(waveVars);
    });

    it('không sinh màu chứa NaN khi dim', () => {
      component.calm = 0.5;
      component.dim = true;

      [component.skyTop, component.skyBottom, component.waterColor, component.figureColor]
        .forEach(color => expect(color).not.toContain('NaN'));
    });
  });

  it('vẽ được và có nhãn trợ năng tiếng Việt', () => {
    component.calm = 1;
    fixture.detectChanges();

    const svg: SVGElement = fixture.nativeElement.querySelector('svg');
    expect(svg).toBeTruthy();
    expect(svg.getAttribute('aria-label')).toContain('hoàng hôn');
  });

  it('gắn class tắt hoạt hoạ khi hệ điều hành yêu cầu giảm chuyển động', () => {
    // reducedMotion đọc matchMedia lúc khởi tạo nên phải giả lập trước khi dựng component
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);

    const reduced = TestBed.createComponent(FishingSceneComponent);
    reduced.detectChanges();

    expect(reduced.componentInstance.reducedMotion).toBeTrue();
    expect(reduced.nativeElement.querySelector('.scene').classList).toContain('reduced-motion');
  });
});
