import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BehaviorSubject } from 'rxjs';
import { HelpComponent } from './help.component';

describe('HelpComponent: mở thẳng một chủ đề qua ?topic=', () => {
  let fixture: ComponentFixture<HelpComponent>;
  let http: HttpTestingController;
  let query: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  const q = (sel: string): HTMLElement | null => fixture.nativeElement.querySelector(sel);

  function setup(topic: string | null) {
    query = new BehaviorSubject(convertToParamMap(topic === null ? {} : { topic }));
    TestBed.configureTestingModule({
      imports: [HelpComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { queryParamMap: query.asObservable(), snapshot: {} } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HelpComponent);
    fixture.detectChanges();
  }

  /** Trả lời các yêu cầu tải .md của mọi chủ đề, mỗi chủ đề một dòng tiêu đề riêng. */
  function flushTopics() {
    http.match(() => true).forEach(req => {
      const id = req.request.url.split('/').pop()!.replace('.md', '');
      req.flush(`# Nội dung ${id}\n\nMột đoạn văn.`);
    });
    fixture.detectChanges();
  }

  beforeEach(() => spyOn(window, 'scrollTo'));
  afterEach(() => fixture?.destroy());

  it('shows the topic list when there is no topic param', () => {
    setup(null);
    flushTopics();

    expect(q('.guide-content')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Phân tích thị trường');
  });

  it('opens topic from query param', () => {
    setup('phan-tich-thi-truong');
    flushTopics();

    const content = q('.guide-content')!;
    expect(content).not.toBeNull();
    expect(content.textContent).toContain('Nội dung phan-tich-thi-truong');
    expect(fixture.nativeElement.textContent).toContain('Quay lại danh sách');
  });

  it('opens the screener topic from its query param', () => {
    setup('sang-loc-co-phieu');
    flushTopics();

    expect(q('.guide-content')!.textContent).toContain('Nội dung sang-loc-co-phieu');
  });

  it('unknown topic shows list', () => {
    setup('khong-co-chu-de-nay');
    flushTopics();

    expect(q('.guide-content')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Hướng dẫn sử dụng');
    expect(fixture.nativeElement.textContent).not.toContain('Quay lại danh sách');
  });

  it('a topic param that arrives after the content has loaded still opens the topic', () => {
    setup(null);
    flushTopics();
    expect(q('.guide-content')).toBeNull();

    query.next(convertToParamMap({ topic: 'quan-ly-rui-ro' }));
    fixture.detectChanges();

    expect(q('.guide-content')!.textContent).toContain('Nội dung quan-ly-rui-ro');
  });

  it('a topic param that arrives before the content has loaded waits for it', () => {
    setup('chien-luoc-giao-dich');
    expect(q('.guide-content')).toBeNull();     // chưa tải xong

    flushTopics();

    expect(q('.guide-content')!.textContent).toContain('Nội dung chien-luoc-giao-dich');
  });
});
