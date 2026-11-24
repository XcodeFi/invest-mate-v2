/**
 * Toán kỳ vọng ở phía giao diện: khớp với backend (`ExpectedValue`) để người dùng thấy EV ngay khi gõ xác suất.
 * Chưa trừ phí: số đã trừ phí ước tính do server tính khi lưu và trả về trong `expectation`.
 */
export interface ScenarioRow {
  label: string;
  /** null = ô trống (chưa ước lượng). Không bao giờ điền sẵn từ tỉ lệ nền (chống neo). */
  probabilityPercent: number | null;
  exitPrice: number;
}

export interface ExitTargetLike {
  price: number;
  percentOfPosition?: number | null;
  actionType?: string;
}

export interface ExpectationResult {
  /** Tổng xác suất đã nhập (ô trống tính là 0). */
  sum: number;
  /** Tổng = 100 ± 0,5. */
  sumOk: boolean;
  /** R của từng dòng (null khi không có 1R hoặc giá thoát không hợp lệ). */
  rowR: (number | null)[];
  riskAmount: number | null;
  evR: number | null;
  evAmount: number | null;
  /** Tổng xác suất (%) các dòng có R > +0,1. */
  impliedWinPercent: number | null;
}

export const PROBABILITY_TOLERANCE = 0.5;
export const DRAW_BAND = 0.1;

export function computeExpectation(rows: ScenarioRow[], entry: number, stopLoss: number, quantity: number): ExpectationResult {
  const sum = rows.reduce((a, r) => a + (r.probabilityPercent ?? 0), 0);
  const sumOk = Math.abs(sum - 100) <= PROBABILITY_TOLERANCE;
  const riskAmount = entry > stopLoss && stopLoss > 0 && quantity > 0 ? (entry - stopLoss) * quantity : null;

  const rowR = rows.map(r =>
    riskAmount !== null && r.exitPrice > 0 ? ((r.exitPrice - entry) * quantity) / riskAmount : null);

  const computable = riskAmount !== null && sumOk && rowR.every(r => r !== null)
    && rows.every(r => (r.probabilityPercent ?? 0) >= 0);
  if (!computable) {
    return { sum, sumOk, rowR, riskAmount, evR: null, evAmount: null, impliedWinPercent: null };
  }

  let ev = 0;
  let win = 0;
  rows.forEach((r, i) => {
    const p = r.probabilityPercent ?? 0;
    ev += (p / 100) * (rowR[i] as number);
    if ((rowR[i] as number) > DRAW_BAND) win += p;
  });
  return { sum, sumOk, rowR, riskAmount, evR: ev, evAmount: ev * (riskAmount as number), impliedWinPercent: win };
}

/** Giá thoát bình quân của các mốc chốt lời theo tỷ lệ vị thế; không có mốc nào dùng được thì null. */
export function blendedExit(targets: ExitTargetLike[]): number | null {
  const usable = targets.filter(t => t.actionType !== 'CutLoss' && t.price > 0 && (t.percentOfPosition ?? 0) > 0);
  if (usable.length === 0) return null;
  const total = usable.reduce((a, t) => a + (t.percentOfPosition as number), 0);
  return usable.reduce((a, t) => a + t.price * (t.percentOfPosition as number), 0) / total;
}

/** Ba dòng dựng sẵn: đạt kế hoạch / cắt lỗ / thoát sớm–hoà, xác suất để trống. Kế hoạch bán không có bảng. */
export function defaultScenarioRows(
  direction: string, entry: number, stopLoss: number, target: number, exitTargets: ExitTargetLike[]
): ScenarioRow[] {
  if (direction !== 'Buy') return [];
  return [
    { label: 'Đạt kế hoạch', probabilityPercent: null, exitPrice: blendedExit(exitTargets) ?? target },
    { label: 'Cắt lỗ', probabilityPercent: null, exitPrice: stopLoss },
    { label: 'Thoát sớm / hoà', probabilityPercent: null, exitPrice: entry },
  ];
}

/**
 * Phần `scenarios` gửi lên server: bảng toàn ô trống thì không gửi (giữ nguyên), trừ khi trước đó đã lưu bảng thì gửi [] để xoá.
 * Có ô nào gõ xác suất thì gửi cả bảng, ô trống tính 0.
 */
export function scenariosPayload(
  rows: ScenarioRow[], hadSavedScenarios: boolean
): { label: string; probabilityPercent: number; exitPrice: number }[] | undefined {
  const typed = rows.some(r => (r.probabilityPercent ?? 0) > 0);
  if (!typed) return hadSavedScenarios ? [] : undefined;
  return rows.map(r => ({ label: r.label, probabilityPercent: r.probabilityPercent ?? 0, exitPrice: r.exitPrice }));
}
