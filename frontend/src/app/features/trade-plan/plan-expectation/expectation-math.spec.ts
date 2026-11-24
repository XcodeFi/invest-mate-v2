import { computeExpectation, defaultScenarioRows, ScenarioRow, scenariosPayload } from './expectation-math';

const row = (label: string, p: number | null, exit: number): ScenarioRow => ({ label, probabilityPercent: p, exitPrice: exit });

describe('expectation-math', () => {
  const base = { entry: 100, stopLoss: 90, quantity: 100 };

  describe('computeExpectation', () => {
    it('computes EV in R and VND plus the implied win probability (35%@120, 15%@134, 50%@90)', () => {
      const r = computeExpectation([row('a', 35, 120), row('b', 15, 134), row('c', 50, 90)], base.entry, base.stopLoss, base.quantity);

      expect(r.sumOk).toBeTrue();
      expect(r.evR).toBeCloseTo(0.71, 4);
      expect(r.evAmount).toBeCloseTo(710, 2);
      expect(r.impliedWinPercent).toBe(50);
      expect(r.rowR[0]).toBeCloseTo(2, 4);
      expect(r.rowR[1]).toBeCloseTo(3.4, 4);
      expect(r.rowR[2]).toBeCloseTo(-1, 4);
      expect(r.riskAmount).toBe(1000);
    });

    it('does not compute EV when probabilities do not sum to 100 (tolerance 0.5)', () => {
      const r = computeExpectation([row('a', 60, 120), row('b', 20, 90)], base.entry, base.stopLoss, base.quantity);

      expect(r.sum).toBe(80);
      expect(r.sumOk).toBeFalse();
      expect(r.evR).toBeNull();
      expect(r.evAmount).toBeNull();
      expect(r.impliedWinPercent).toBeNull();
    });

    it('accepts a sum within 0.5 of 100 and rejects just outside', () => {
      expect(computeExpectation([row('a', 49.7, 120), row('b', 50, 90)], 100, 90, 100).sumOk).toBeTrue();
      expect(computeExpectation([row('a', 49.4, 120), row('b', 50, 90)], 100, 90, 100).sumOk).toBeFalse();
    });

    it('empty probabilities count as zero and never produce a number', () => {
      const r = computeExpectation([row('a', null, 120), row('b', null, 90)], 100, 90, 100);

      expect(r.sum).toBe(0);
      expect(r.evR).toBeNull();
    });

    it('still shows per-row R when only the sum is wrong', () => {
      const r = computeExpectation([row('a', 10, 120), row('b', null, 90)], 100, 90, 100);

      expect(r.rowR[0]).toBeCloseTo(2, 4);
      expect(r.rowR[1]).toBeCloseTo(-1, 4);
    });

    it('returns null EV (never a division by zero) when there is no 1R', () => {
      const r = computeExpectation([row('a', 100, 120)], 100, 100, 100);

      expect(r.evR).toBeNull();
      expect(r.riskAmount).toBeNull();
      expect(r.rowR[0]).toBeNull();
    });

    it('returns null EV when an exit price is not positive', () => {
      expect(computeExpectation([row('a', 50, 120), row('b', 50, 0)], 100, 90, 100).evR).toBeNull();
      expect(computeExpectation([row('a', 50, 120), row('b', 50, -5)], 100, 90, 100).evR).toBeNull();
    });

    it('returns null when quantity is missing', () => {
      expect(computeExpectation([row('a', 100, 120)], 100, 90, 0).evR).toBeNull();
    });

    it('treats a net R within ±0.1 as not a win', () => {
      const r = computeExpectation([row('a', 50, 101), row('b', 50, 90)], 100, 90, 100);   // +0,1R: vùng hoà

      expect(r.impliedWinPercent).toBe(0);
    });
  });

  describe('defaultScenarioRows', () => {
    it('builds three rows with empty probabilities (no anchoring)', () => {
      const rows = defaultScenarioRows('Buy', 100, 90, 130, []);

      expect(rows.map(x => x.label)).toEqual(['Đạt kế hoạch', 'Cắt lỗ', 'Thoát sớm / hoà']);
      expect(rows.every(x => x.probabilityPercent === null)).toBeTrue();
      expect(rows.map(x => x.exitPrice)).toEqual([130, 90, 100]);
    });

    it('uses the weighted exit-target price for the planned row', () => {
      const rows = defaultScenarioRows('Buy', 100, 90, 130, [
        { price: 110, percentOfPosition: 50, actionType: 'TakeProfit' },
        { price: 120, percentOfPosition: 50, actionType: 'TakeProfit' },
      ]);

      expect(rows[0].exitPrice).toBe(115);
    });

    it('builds nothing for sell plans', () => {
      expect(defaultScenarioRows('Sell', 100, 110, 80, [])).toEqual([]);
    });
  });

  describe('scenariosPayload', () => {
    it('sends nothing when every probability is empty and nothing was saved before', () => {
      expect(scenariosPayload([row('a', null, 120), row('b', 0, 90)], false)).toBeUndefined();
    });

    it('sends an empty list to clear a previously saved table', () => {
      expect(scenariosPayload([row('a', null, 120)], true)).toEqual([]);
    });

    it('sends the rows with empty probabilities as zero once any probability is typed', () => {
      expect(scenariosPayload([row('a', 60, 120), row('b', null, 90)], false)).toEqual([
        { label: 'a', probabilityPercent: 60, exitPrice: 120 },
        { label: 'b', probabilityPercent: 0, exitPrice: 90 },
      ]);
    });
  });
});
