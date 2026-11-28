import { THEME_STORAGE_KEY } from './theme.service';

/**
 * Script chạy trước Angular trong index.html (để trang không nháy sáng rồi mới tối). Test đọc đúng file index.html thật
 * (karma phục vụ nó ở /theme-fixtures, xem angular.json) và CHẠY đoạn script đó với storage giả.
 */
describe('index.html pre-boot theme script', () => {
  let html = '';
  let script = '';

  beforeAll(async () => {
    const res = await fetch('/theme-fixtures/index.html');
    expect(res.ok).withContext('index.html phải được karma phục vụ ở /theme-fixtures').toBeTrue();
    html = await res.text();
    const m = Array.from(html.matchAll(/<script>([\s\S]*?)<\/script>/g)).map(x => x[1]).find(s => s.includes(THEME_STORAGE_KEY));
    script = m ?? '';
  });

  function run(storage: { getItem: (k: string) => string | null } | 'blocked'): string[] {
    const added: string[] = [];
    const fakeDocument = { documentElement: { classList: { add: (c: string) => added.push(c) } } };
    const fakeStorage = storage === 'blocked' ? { getItem: () => { throw new DOMException('chặn', 'SecurityError'); } } : storage;
    new Function('localStorage', 'document', script)(fakeStorage, fakeDocument);
    return added;
  }

  it('the script exists and uses the same storage key as ThemeService', () => {
    expect(script).withContext('không thấy script đặt chế độ giao diện trong index.html').not.toBe('');
    expect(script).toContain(`'${THEME_STORAGE_KEY}'`);
  });

  it('runs in <head>, before the stylesheet links and the app body', () => {
    const at = html.indexOf(THEME_STORAGE_KEY);
    expect(at).toBeGreaterThan(-1);
    expect(at).toBeLessThan(html.indexOf('<body'));
    expect(at).toBeLessThan(html.indexOf('fonts.googleapis.com'));
    expect(html.indexOf('<body')).toBeGreaterThan(-1);
  });

  it('theme_defaults_to_dark_when_storage_empty', () => {
    expect(run({ getItem: () => null })).toEqual(['dark']);
  });

  it('theme_defaults_to_dark_when_storage_blocked', () => {
    expect(run('blocked')).toEqual(['dark']);
  });

  it('keeps light only when the saved value is exactly "light"', () => {
    expect(run({ getItem: () => 'light' })).toEqual([]);
    expect(run({ getItem: () => 'dark' })).toEqual(['dark']);
    expect(run({ getItem: () => 'whatever' })).toEqual(['dark']);
  });
});
