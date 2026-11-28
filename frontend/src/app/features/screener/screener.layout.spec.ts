import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ScreenerComponent } from './screener.component';
import { MarketDataService } from '../../core/services/market-data.service';

/**
 * Bố cục ô lọc: nhãn dài xuống hai dòng thì ô nhập của nó KHÔNG được tụt xuống so với các ô cùng hàng. Điểm ngắt của Tailwind (`lg:`) tính theo
 * khung nhìn, nên trang được dựng trong một iframe có độ rộng cố định (khung Karma mặc định hẹp), kèm bản sao các thẻ style của trang.
 */
describe('ScreenerComponent: căn hàng của các ô lọc', () => {
  const INPUTS = ['screener-min-adtv', 'screener-min-price', 'screener-min-cap', 'screener-min-roe', 'screener-max-pe', 'screener-min-rs3m'];
  let fixture: ComponentFixture<ScreenerComponent>;
  let frame: HTMLIFrameElement;

  async function renderAt(width: number, dark = false): Promise<Document> {
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getScreener']);
    market.getScreener.and.returnValue(of({ status: 'unavailable', reason: 'x', universeCount: 0, passed: [], unverifiedHose: [], excludedCounts: {}, sectorCounts: {}, tradeoffCount: 0, notes: [], stale: false, hoseListsStale: false, disclaimer: '' } as any));
    TestBed.configureTestingModule({ imports: [ScreenerComponent], providers: [provideRouter([]), { provide: MarketDataService, useValue: market }] });
    fixture = TestBed.createComponent(ScreenerComponent);
    fixture.detectChanges();

    frame = document.createElement('iframe');
    frame.style.cssText = `position:absolute;left:-10000px;top:0;width:${width}px;height:1200px;border:0`;
    document.body.appendChild(frame);
    const doc = frame.contentDocument!;
    doc.open();
    doc.write('<!doctype html><html><head></head><body></body></html>');
    doc.close();
    const loads: Promise<void>[] = [];
    document.head.querySelectorAll('style, link[rel="stylesheet"]').forEach(node => {
      const copy = node.cloneNode(true) as HTMLElement;
      if (copy.tagName === 'LINK') loads.push(new Promise(res => { copy.addEventListener('load', () => res()); copy.addEventListener('error', () => res()); }));
      doc.head.appendChild(copy);
    });
    await Promise.all(loads);
    if (dark) doc.documentElement.classList.add('dark');
    doc.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    return doc;
  }

  const tops = (doc: Document) => INPUTS.map(id => (doc.querySelector(`[data-testid="${id}"]`) as HTMLElement).getBoundingClientRect().top);

  afterEach(() => { fixture?.destroy(); frame?.remove(); });

  it('at 1400px (6 columns) all six inputs sit on the same line even though two of the labels wrap to two lines', async () => {
    const doc = await renderAt(1400);
    const t = tops(doc);
    expect(Math.max(...t) - Math.min(...t)).withContext(`top của sáu ô: ${t.join(', ')}`).toBeLessThanOrEqual(1);
  });

  it('at 1980px in dark mode the inputs are still aligned', async () => {
    const doc = await renderAt(1980, true);

    const t = tops(doc);
    expect(Math.max(...t) - Math.min(...t)).toBeLessThanOrEqual(1);
  });

  it('at 400px (2 columns) the two inputs of each row are aligned', async () => {
    const doc = await renderAt(400);
    const t = tops(doc);

    for (let i = 0; i < t.length; i += 2) expect(Math.abs(t[i] - t[i + 1])).withContext(`hàng ${i / 2 + 1}: ${t[i]} vs ${t[i + 1]}`).toBeLessThanOrEqual(1);
  });

  it('at 800px (3 columns) the inputs of each row are aligned', async () => {
    const doc = await renderAt(800);
    const t = tops(doc);

    for (let i = 0; i < t.length; i += 3) {
      const row = t.slice(i, i + 3);
      expect(Math.max(...row) - Math.min(...row)).withContext(`hàng ${i / 3 + 1}: ${row.join(', ')}`).toBeLessThanOrEqual(1);
    }
  });
});
