import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { MoodService, TodayMoodDto } from '../../../core/services/mood.service';
import { TradeService } from '../../../core/services/trade.service';
import { PatienceHeroComponent } from './patience-hero.component';
import { QUOTES } from './patience-quotes';

describe('PatienceHeroComponent', () => {
  let moodService: jasmine.SpyObj<MoodService>;
  let tradeService: jasmine.SpyObj<TradeService>;

  function build(
    activity: { lastTradeDate: string | null; daysSince: number | null },
    today: TodayMoodDto
  ): ComponentFixture<PatienceHeroComponent> {
    tradeService.getLastActivity.and.returnValue(of(activity));
    moodService.getToday.and.returnValue(of(today));
    const fixture = TestBed.createComponent(PatienceHeroComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    moodService = jasmine.createSpyObj('MoodService', ['getToday', 'setMood', 'markOverride']);
    tradeService = jasmine.createSpyObj('TradeService', ['getLastActivity']);
    moodService.setMood.and.returnValue(of(void 0));
    moodService.markOverride.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PatienceHeroComponent],
      providers: [
        { provide: MoodService, useValue: moodService },
        { provide: TradeService, useValue: tradeService },
      ],
    }).compileComponents();
  });

  describe('đồng hồ kiên nhẫn', () => {
    it('hiện số ngày thật, không bị cắt ở trần 14 ngày của hình ảnh', () => {
      const fixture = build({ lastTradeDate: '2026-01-05', daysSince: 200 }, { mood: null, overrode: false });

      const text = fixture.nativeElement.querySelector('[data-test="patience-counter"]').textContent;
      expect(text).toContain('200');
      expect(fixture.componentInstance.calm).toBe(1);
    });

    it('chưa có lệnh nào thì nói thẳng, không bịa số ngày', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: null, overrode: false });

      const text = fixture.nativeElement.querySelector('[data-test="patience-counter"]').textContent;
      expect(text).toContain('Chưa có lệnh nào');
      expect(text).not.toContain('0 ngày');
    });

    it('vừa đặt lệnh hôm nay thì mặt nước động nhất', () => {
      const fixture = build({ lastTradeDate: '2026-08-11', daysSince: 0 }, { mood: null, overrode: false });

      expect(fixture.componentInstance.calm).toBe(0);
      expect(fixture.nativeElement.querySelector('[data-test="patience-counter"]').textContent)
        .toContain('Hôm nay vừa đặt lệnh');
    });

    it('API lỗi thì không vỡ trang, chỉ không có số ngày', () => {
      tradeService.getLastActivity.and.returnValue(throwError(() => new Error('500')));
      moodService.getToday.and.returnValue(of({ mood: null, overrode: false }));

      const fixture = TestBed.createComponent(PatienceHeroComponent);
      fixture.detectChanges();

      expect(fixture.componentInstance.daysSince).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-test="patience-counter"]')).toBeTruthy();
    });
  });

  describe('chấm tâm trạng', () => {
    it('chưa chấm thì hiện bảng chọn và dùng nhóm châm ngôn Bình tĩnh', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: null, overrode: false });

      expect(fixture.nativeElement.querySelector('[data-test="mood-Fomo"]')).toBeTruthy();
      expect(QUOTES.Calm).toContain(fixture.componentInstance.quote);
    });

    it('bấm một trạng thái thì gọi API, thu bảng chọn lại và phát sự kiện', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: null, overrode: false });
      const emitted: TodayMoodDto[] = [];
      fixture.componentInstance.moodChange.subscribe(e => emitted.push(e));

      fixture.nativeElement.querySelector('[data-test="mood-Fomo"]').click();
      fixture.detectChanges();

      expect(moodService.setMood).toHaveBeenCalledWith('Fomo');
      expect(emitted).toEqual([{ mood: 'Fomo', overrode: false }]);
      expect(fixture.nativeElement.querySelector('[data-test="mood-current"]').textContent).toContain('FOMO');
    });

    it('đã chấm từ trước thì hiện trạng thái kèm nút đổi, không hỏi lại', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Revenge', overrode: false });

      expect(fixture.nativeElement.querySelector('[data-test="mood-current"]').textContent).toContain('Cay cú');
      expect(fixture.nativeElement.querySelector('[data-test="mood-Fomo"]')).toBeNull();
    });

    it('châm ngôn đổi theo trạng thái đã chấm', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Revenge', overrode: false });

      expect(QUOTES.Revenge).toContain(fixture.componentInstance.quote);
    });

    it('trạng thái khác Bình tĩnh thì làm tối cảnh câu', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Fear', overrode: false });

      expect(fixture.componentInstance.isEmotional).toBeTrue();
    });

    it('Bình tĩnh KHÔNG làm tối cảnh câu', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Calm', overrode: false });

      expect(fixture.componentInstance.isEmotional).toBeFalse();
    });
  });

  describe('nút "đổi" không được thành đường thoát', () => {
    it('mở lại bảng chọn nhưng vẫn giữ trạng thái đang có cảm xúc', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Fomo', overrode: false });

      fixture.nativeElement.querySelector('[data-test="mood-change"]').click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-test="mood-Calm"]')).toBeTruthy();
      expect(fixture.componentInstance.mood).toBe('Fomo');
      expect(fixture.componentInstance.isEmotional).toBeTrue();
    });

    it('mở lại bảng chọn KHÔNG phát sự kiện gỡ lớp phủ', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Fomo', overrode: false });
      const emitted: TodayMoodDto[] = [];
      fixture.componentInstance.moodChange.subscribe(e => emitted.push(e));

      fixture.nativeElement.querySelector('[data-test="mood-change"]').click();
      fixture.detectChanges();

      expect(emitted).toEqual([]);
    });

    it('đổi qua Bình tĩnh rồi quay lại FOMO thì mất quyền bỏ qua lớp phủ', () => {
      const fixture = build({ lastTradeDate: null, daysSince: null }, { mood: 'Fomo', overrode: true });
      const emitted: TodayMoodDto[] = [];
      fixture.componentInstance.moodChange.subscribe(e => emitted.push(e));

      fixture.componentInstance.choose('Calm');
      fixture.componentInstance.choose('Fomo');

      expect(emitted[emitted.length - 1]).toEqual({ mood: 'Fomo', overrode: false });
    });
  });
});
