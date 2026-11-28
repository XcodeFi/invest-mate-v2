/**
 * Kiểm CSS thật được áp dụng (styles.css + dark-theme.css đều nằm trong bản build test): gắn phần tử mẫu với các class Tailwind
 * sáng vào trang, bật/tắt class `dark` trên <html> và đọc màu tính toán.
 */
describe('dark-theme.css applied to the page', () => {
  let host: HTMLDivElement;
  const hadDark = document.documentElement.classList.contains('dark');

  beforeEach(() => {
    host = document.createElement('div');
    document.body.appendChild(host);
  });

  afterEach(() => {
    host.remove();
    document.documentElement.classList.toggle('dark', hadDark);
  });

  function el(html: string): HTMLElement {
    host.innerHTML = html;
    return host.firstElementChild as HTMLElement;
  }

  const css = (e: Element, prop: string) => getComputedStyle(e).getPropertyValue(prop).trim();

  it('light mode is untouched: the same classes keep their original Tailwind colours', () => {
    document.documentElement.classList.remove('dark');
    const card = el('<div class="bg-white text-gray-500 border border-gray-300">x</div>');

    expect(css(card, 'background-color')).toBe('rgb(255, 255, 255)');
    expect(css(card, 'color')).toBe('rgb(107, 114, 128)');
    expect(css(card, 'border-top-color')).toBe('rgb(209, 213, 219)');
  });

  it('dark mode remaps the surface, secondary text and border', () => {
    document.documentElement.classList.add('dark');
    const card = el('<div class="bg-white text-gray-500 border border-gray-300">x</div>');

    expect(css(card, 'background-color')).toBe('rgb(17, 24, 39)');
    expect(css(card, 'color')).toBe('rgb(148, 163, 184)');
    expect(css(card, 'border-top-color')).toBe('rgb(51, 65, 85)');
  });

  it('dark mode: page background, main text and the stronger text levels', () => {
    document.documentElement.classList.add('dark');

    expect(css(el('<div class="bg-gray-50">x</div>'), 'background-color')).toBe('rgb(11, 18, 32)');
    expect(css(el('<p class="text-gray-900">x</p>'), 'color')).toBe('rgb(229, 231, 235)');
    expect(css(el('<p class="text-gray-700">x</p>'), 'color')).toBe('rgb(203, 213, 225)');
    expect(css(el('<p class="text-gray-400">x</p>'), 'color')).toBe('rgb(139, 153, 174)');
  });

  it('dark mode: translucent light variants are remapped too (bg-white/60, bg-indigo-50/60, bg-blue-50/40, border-*-200/60)', () => {
    document.documentElement.classList.add('dark');

    expect(css(el('<div class="bg-white/60">x</div>'), 'background-color')).toBe('rgba(17, 24, 39, 0.6)');
    expect(css(el('<div class="bg-white/70">x</div>'), 'background-color')).toBe('rgba(17, 24, 39, 0.7)');
    expect(css(el('<div class="bg-white/85">x</div>'), 'background-color')).toBe('rgba(17, 24, 39, 0.85)');
    expect(css(el('<div class="bg-indigo-50/60">x</div>'), 'background-color')).toBe('rgba(99, 102, 241, 0.1)');
    expect(css(el('<div class="bg-blue-50/40">x</div>'), 'background-color')).toBe('rgba(59, 130, 246, 0.1)');
    expect(css(el('<div class="bg-blue-50/30">x</div>'), 'background-color')).toBe('rgba(59, 130, 246, 0.1)');
    expect(css(el('<div class="border border-blue-200/60">x</div>'), 'border-top-color')).toBe('rgba(59, 130, 246, 0.35)');
    expect(css(el('<div class="border border-emerald-200/60">x</div>'), 'border-top-color')).toBe('rgba(16, 185, 129, 0.35)');
  });

  it('light mode: the same translucent classes keep their Tailwind look (no dark rule leaks)', () => {
    document.documentElement.classList.remove('dark');

    expect(css(el('<div class="bg-white/60">x</div>'), 'background-color')).toBe('rgba(255, 255, 255, 0.6)');
  });

  it('dark mode: status panels become translucent tints with lightened text, keeping their meaning', () => {
    document.documentElement.classList.add('dark');
    const panel = el('<div class="bg-red-100 text-red-700 border border-red-200">x</div>');

    expect(css(panel, 'background-color')).toBe('rgba(239, 68, 68, 0.16)');
    expect(css(panel, 'color')).toBe('rgb(248, 113, 113)');   // red-400
    expect(css(panel, 'border-top-color')).toBe('rgba(239, 68, 68, 0.35)');
    expect(css(el('<span class="text-green-600">x</span>'), 'color')).toBe('rgb(74, 222, 128)');
  });

  it('dark mode: solid action colours stay as they are (white text on a blue button)', () => {
    document.documentElement.classList.add('dark');
    const button = el('<button class="bg-blue-600 text-white">x</button>');

    expect(css(button, 'background-color')).toBe('rgb(37, 99, 235)');
    expect(css(button, 'color')).toBe('rgb(255, 255, 255)');
  });

  it('dark mode: form controls without their own colours get a dark background and light text; coloured ones keep their tint', () => {
    document.documentElement.classList.add('dark');
    host.innerHTML = '<input type="text"><input type="text" class="bg-red-50 border-red-300">';
    const [plain, error] = Array.from(host.children);

    expect(css(plain, 'background-color')).toBe('rgb(11, 18, 32)');
    expect(css(plain, 'color')).toBe('rgb(229, 231, 235)');
    expect(css(error, 'background-color')).toBe('rgba(239, 68, 68, 0.1)');
  });

  it('dark mode: native form controls follow the dark colour scheme (date pickers, scrollbars)', () => {
    document.documentElement.classList.add('dark');

    expect(css(document.documentElement, 'color-scheme')).toBe('dark');
  });

  it('dark mode: component classes compiled from @apply are remapped too (.card, .btn-secondary)', () => {
    document.documentElement.classList.add('dark');

    expect(css(el('<div class="card">x</div>'), 'background-color')).toBe('rgb(17, 24, 39)');
    expect(css(el('<button class="btn-secondary">x</button>'), 'background-color')).toBe('rgb(39, 52, 73)');
  });

  it('dark mode: the body text defaults to the light colour', () => {
    document.documentElement.classList.add('dark');

    expect(css(document.body, 'color')).toBe('rgb(229, 231, 235)');
  });
});
