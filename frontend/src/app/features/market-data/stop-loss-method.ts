/** Nhãn cách tính cắt lỗ của gợi ý giao dịch (backend: suggestedStopLossMethod). Không biết thì rỗng. */
export function stopLossMethodLabel(method: string | null | undefined): string {
  switch (method) {
    case 'support2': return 'theo hỗ trợ thứ 2';
    case 'atr2x': return 'theo 2×ATR';
    case 'percent5': return '−5% dưới hỗ trợ';
    default: return '';
  }
}

/** R:R dưới 1: rủi ro lớn hơn lợi nhuận kỳ vọng, không đủ hấp dẫn. Không biết thì không cảnh báo. */
export function isLowRiskReward(rr: number | null | undefined): boolean {
  return rr != null && rr < 1;
}

export const LOW_RR_NOTE = 'R:R thấp — không đủ hấp dẫn';
