import { INDICATOR_GUIDES } from './indicator-guides';
import { TaGroup } from './ta-group-layers';

/** indicator-guides.ts và phan-tich-thi-truong.md (Help) phải nói cùng một nội dung: cách đọc và lý do, nhà đầu tư, nguồn, lưu ý. */
describe('INDICATOR_GUIDES khớp hướng dẫn Phân tích thị trường (Help)', () => {
  let doc = '';
  const groups = Object.keys(INDICATOR_GUIDES) as TaGroup[];

  beforeAll(async () => {
    const res = await fetch('/assets/docs/phan-tich-thi-truong.md');
    expect(res.ok).withContext('tải được tệp Help').toBeTrue();
    doc = await res.text();
  });

  it('every how and why of every howToRead appears verbatim in the Help doc', () => {
    groups.forEach(g => INDICATOR_GUIDES[g].items.forEach(item => item.howToRead.forEach((r, i) => {
      expect(doc).withContext(`${g}/${item.name} how ${i}`).toContain(r.how);
      expect(doc).withContext(`${g}/${item.name} why ${i}`).toContain(r.why);
    })));
  });

  it('every investor entry (who, how, why, source, note) and every noUsers line appears verbatim in the Help doc', () => {
    groups.forEach(g => INDICATOR_GUIDES[g].items.forEach(item => {
      if (item.noUsers) expect(doc).withContext(`${g}/${item.name} noUsers`).toContain(item.noUsers);
      item.usedBy.forEach(u => [u.who, u.how, u.why, u.source, u.note ?? ''].forEach(t => expect(doc).withContext(`${g}/${item.name}/${u.who}`).toContain(t)));
    }));
  });
});
