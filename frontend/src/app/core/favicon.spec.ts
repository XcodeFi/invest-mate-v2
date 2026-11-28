/**
 * Biểu tượng tab trình duyệt: đọc đúng index.html (karma phục vụ ở /theme-fixtures, xem angular.json) và kiểm MỌI <link rel="icon"> trỏ tới
 * một file có thật, phục vụ được. Trước đây trỏ tới favicon.ico không tồn tại nên tab hiện biểu tượng mặc định (quả cầu).
 */
describe('favicon của trang', () => {
  const iconLinks = async () => {
    const res = await fetch('/theme-fixtures/index.html');
    expect(res.ok).withContext('index.html phải được karma phục vụ ở /theme-fixtures').toBeTrue();
    const doc = new DOMParser().parseFromString(await res.text(), 'text/html');
    return Array.from(doc.querySelectorAll('link[rel="icon"]')) as HTMLLinkElement[];
  };

  it('khai báo biểu tượng SVG và PNG dự phòng', async () => {
    const links = await iconLinks();
    const types = links.map(l => l.getAttribute('type'));

    expect(types).toContain('image/svg+xml');
    expect(types).toContain('image/png');
  });

  it('mọi biểu tượng được khai báo đều tồn tại và đúng loại file (không còn trỏ vào favicon.ico thiếu)', async () => {
    const links = await iconLinks();
    expect(links.length).toBeGreaterThan(0);

    for (const link of links) {
      const href = link.getAttribute('href')!;
      expect(href).withContext('không dùng favicon.ico (file không có trong repo)').not.toContain('favicon.ico');
      const res = await fetch('/' + href.replace(/^\//, ''));
      expect(res.ok).withContext(href).toBeTrue();
      expect(res.headers.get('content-type') ?? '').withContext(href).toContain(link.getAttribute('type')!);
    }
  });

  it('biểu tượng SVG là vector thật (có viewBox, không bọc ảnh bitmap)', async () => {
    const svg = (await iconLinks()).find(l => l.getAttribute('type') === 'image/svg+xml')!;
    const text = await (await fetch('/' + svg.getAttribute('href')!.replace(/^\//, ''))).text();

    expect(text).toContain('<svg');
    expect(text).toContain('viewBox');
    expect(text).not.toContain('<image');
    expect(text).not.toContain('data:image/png');
  });
});
