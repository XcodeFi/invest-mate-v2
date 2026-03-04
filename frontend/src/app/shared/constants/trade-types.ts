// Trade type constants and types for consistent usage across the application
export enum TradeType {
  BUY = 'BUY',
  SELL = 'SELL'
}

export const TradeTypeDisplay = {
  [TradeType.BUY]: 'Mua',
  [TradeType.SELL]: 'Bán'
} as const;

export const TradeTypeClass = {
  [TradeType.BUY]: 'bg-green-100 text-green-800',
  [TradeType.SELL]: 'bg-red-100 text-red-800'
} as const;

// Filter options for trade type dropdowns
export interface TradeTypeFilterOption {
  value: string;
  label: string;
  displayText: string;
  className: string;
}

export const TRADE_TYPE_FILTER_OPTIONS: TradeTypeFilterOption[] = [
  {
    value: TradeType.BUY,
    label: 'Mua',
    displayText: TradeTypeDisplay[TradeType.BUY],
    className: TradeTypeClass[TradeType.BUY]
  },
  {
    value: TradeType.SELL,
    label: 'Bán',
    displayText: TradeTypeDisplay[TradeType.SELL],
    className: TradeTypeClass[TradeType.SELL]
  }
];

// Utility functions
export const isBuyTrade = (tradeType: string): boolean => {
  return tradeType.toUpperCase() === TradeType.BUY;
};

export const isSellTrade = (tradeType: string): boolean => {
  return tradeType.toUpperCase() === TradeType.SELL;
};

export const getTradeTypeDisplay = (tradeType: string): string => {
  const upperCaseType = tradeType.toUpperCase();
  return TradeTypeDisplay[upperCaseType as TradeType] || tradeType;
};

export const getTradeTypeClass = (tradeType: string): string => {
  const upperCaseType = tradeType.toUpperCase();
  return TradeTypeClass[upperCaseType as TradeType] || 'bg-gray-100 text-gray-800';
};