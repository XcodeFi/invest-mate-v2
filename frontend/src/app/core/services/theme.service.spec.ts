import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { resolveTheme, THEME_STORAGE_KEY, ThemeService } from './theme.service';

/** Document giả: chỉ có cái ThemeService đụng tới (class của <html> và localStorage). */
function fakeDocument(storage: { getItem: (k: string) => string | null; setItem: (k: string, v: string) => void } | 'blocked') {
  const classes = new Set<string>();
  const doc = {
    documentElement: {
      classList: {
        contains: (c: string) => classes.has(c),
        toggle: (c: string, force?: boolean) => { (force ?? !classes.has(c)) ? classes.add(c) : classes.delete(c); },
        add: (c: string) => classes.add(c),
        remove: (c: string) => classes.delete(c)
      }
    },
    get defaultView() {
      if (storage === 'blocked') {
        return { get localStorage(): never { throw new DOMException('Storage bị chặn', 'SecurityError'); } };
      }
      return { localStorage: storage };
    }
  };
  return { doc, classes };
}

function memoryStorage(initial: Record<string, string> = {}) {
  const data = { ...initial };
  return {
    data,
    getItem: (k: string) => (k in data ? data[k] : null),
    setItem: (k: string, v: string) => { data[k] = v; }
  };
}

function create(doc: unknown): ThemeService {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: doc }] });
  return TestBed.inject(ThemeService);
}

describe('ThemeService', () => {
  it('theme_defaults_to_dark_when_storage_empty', () => {
    const { doc, classes } = fakeDocument(memoryStorage());

    const svc = create(doc);

    expect(svc.theme()).toBe('dark');
    expect(classes.has('dark')).toBeTrue();
  });

  it('theme_defaults_to_dark_when_storage_blocked', () => {
    const { doc, classes } = fakeDocument('blocked');

    const svc = create(doc);

    expect(svc.theme()).toBe('dark');
    expect(classes.has('dark')).toBeTrue();
  });

  it('keeps the saved light choice', () => {
    const { doc, classes } = fakeDocument(memoryStorage({ [THEME_STORAGE_KEY]: 'light' }));

    const svc = create(doc);

    expect(svc.theme()).toBe('light');
    expect(classes.has('dark')).toBeFalse();
  });

  it('treats an unknown stored value as the dark default', () => {
    expect(resolveTheme('purple')).toBe('dark');
    expect(resolveTheme('')).toBe('dark');
    expect(resolveTheme(null)).toBe('dark');
    expect(resolveTheme(undefined)).toBe('dark');
    expect(resolveTheme('light')).toBe('light');
    expect(resolveTheme('dark')).toBe('dark');
  });

  it('toggle_switches_class_and_persists', () => {
    const storage = memoryStorage();
    const { doc, classes } = fakeDocument(storage);
    const svc = create(doc);

    svc.toggle();
    expect(svc.theme()).toBe('light');
    expect(classes.has('dark')).toBeFalse();
    expect(storage.data[THEME_STORAGE_KEY]).toBe('light');

    svc.toggle();
    expect(svc.theme()).toBe('dark');
    expect(classes.has('dark')).toBeTrue();
    expect(storage.data[THEME_STORAGE_KEY]).toBe('dark');
  });

  it('toggle still switches the class when the storage write throws', () => {
    const { doc, classes } = fakeDocument('blocked');
    const svc = create(doc);

    expect(() => svc.toggle()).not.toThrow();

    expect(svc.theme()).toBe('light');
    expect(classes.has('dark')).toBeFalse();
  });

  it('exposes the tooltip text for the toggle in Vietnamese', () => {
    const { doc } = fakeDocument(memoryStorage());
    const svc = create(doc);

    expect(svc.toggleLabel()).toBe('Chế độ sáng');   // đang tối: nút đưa sang sáng
    svc.toggle();
    expect(svc.toggleLabel()).toBe('Chế độ tối');
  });
});
