import { TA_GROUPS } from './ta-group-layers';
import { firstStarredGroup, GROUP_STARS_KEY, orderByStars, readGroupStars, writeGroupStars } from './ta-group-stars';

describe('ta-group-stars', () => {
  beforeEach(() => localStorage.removeItem(GROUP_STARS_KEY));
  afterEach(() => localStorage.removeItem(GROUP_STARS_KEY));

  it('nothing starred by default', () => {
    expect(readGroupStars()).toEqual([]);
    expect(firstStarredGroup()).toBeNull();
  });

  it('round-trips, in TA_GROUPS order whatever order they were written in', () => {
    writeGroupStars(['moc-gia', 'xu-huong']);

    expect(readGroupStars()).toEqual(['xu-huong', 'moc-gia']);
    expect(firstStarredGroup()).toBe('xu-huong');
  });

  it('ignores unknown keys, corrupt JSON and non-arrays', () => {
    localStorage.setItem(GROUP_STARS_KEY, JSON.stringify(['xu-huong', 'khong-co', 7]));
    expect(readGroupStars()).toEqual(['xu-huong']);
    localStorage.setItem(GROUP_STARS_KEY, '{oops');
    expect(readGroupStars()).toEqual([]);
    localStorage.setItem(GROUP_STARS_KEY, '{"a":1}');
    expect(readGroupStars()).toEqual([]);
  });

  it('never throws when storage is blocked', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('blocked');
    spyOn(Storage.prototype, 'setItem').and.throwError('blocked');

    expect(readGroupStars()).toEqual([]);
    expect(() => writeGroupStars(['xu-huong'])).not.toThrow();
  });

  it('starred groups come first and each part keeps the TA_GROUPS order', () => {
    const order = orderByStars(TA_GROUPS, ['moc-gia', 'dong-luong']).map(g => g.key);

    expect(order).toEqual(['dong-luong', 'moc-gia', 'tong-hop', 'xu-huong', 'bien-dong', 'dong-tien']);
  });
});
