export interface TimeHorizonOption {
  value: string;
  label: string;
}

export const TIME_HORIZON_OPTIONS: TimeHorizonOption[] = [
  { value: 'ShortTerm', label: 'Ngắn hạn (< 3 tháng)' },
  { value: 'MediumTerm', label: 'Trung hạn (3-12 tháng)' },
  { value: 'LongTerm', label: 'Dài hạn (> 1 năm)' },
];

export const DEFAULT_TIME_HORIZON = 'MediumTerm';
