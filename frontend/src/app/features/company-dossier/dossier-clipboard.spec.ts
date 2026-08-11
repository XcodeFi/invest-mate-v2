import { buildAiPrompt, parseAiPayload } from './dossier-clipboard';
import { CompanyFundamentals } from '../../core/services/market-data.service';

const CONTENT = {
  symbol: 'EVF',
  businessModel: 'Cho thuê tài chính cho doanh nghiệp vừa và nhỏ.',
  moats: [{ description: 'Chi phí vốn thấp nhờ cổ đông ngân hàng' }, { description: '   ' }],
  riskFactors: [
    { rank: 1, description: 'Nợ xấu tăng', observableSignal: 'NPL vượt 3% hai quý', isDealBreaker: true, suggestedTrigger: 'EarningsMiss' },
    { rank: 2, description: 'Lãi suất tăng', observableSignal: 'NIM giảm 50bps', isDealBreaker: false, suggestedTrigger: null },
  ],
  notes: 'Theo dõi room ngoại',
};

const FUNDAMENTALS = {
  symbol: 'EVF',
  company: {
    companyName: 'Công ty Tài chính EVN', shortName: null, exchange: 'HOSE', industry: 'Tài chính',
    majorShareholders: [{ name: 'EVN', position: null, quantity: 1, percentage: 45 }],
    leaders: [], listedShares: null, outstandingShares: null, freeFloatRate: null,
  },
  indicators: {
    pe: 12.5, pb: 1.2, eps: 1500, roe: 14, roa: 2.1, marketCap: null, bookValue: null,
    beta: 1.1, min52W: 10, max52W: 18, industryGroup: null, auditFirmName: 'Deloitte', auditIsBig4: true,
  },
  incomeStatements: [{ period: 'Q1/2026', revenue: 1200, netProfit: 300, grossProfit: null }],
  peers: [],
  dividendEvents: [],
  businessPlan: null,
  unavailableSections: ['peers', 'dividendEvents', 'businessPlan'],
} as CompanyFundamentals;

describe('buildAiPrompt', () => {
  it('gói đủ nội dung hồ sơ để AI soát được', () => {
    const text = buildAiPrompt(CONTENT, FUNDAMENTALS);

    expect(text).toContain('Hồ sơ công ty: EVF');
    expect(text).toContain('Cho thuê tài chính cho doanh nghiệp vừa và nhỏ.');
    expect(text).toContain('NPL vượt 3% hai quý');
    expect(text).toContain('YẾU TỐ HỦY DIỆT');
    expect(text).toContain('Theo dõi room ngoại');
  });

  it('kèm schema JSON để AI trả về đúng shape của upsert_company_dossier', () => {
    const text = buildAiPrompt(CONTENT, FUNDAMENTALS);

    expect(text).toContain('```json');
    expect(text).toContain('observableSignal');
    expect(text).toContain('isDealBreaker');
    expect(text).toContain('suggestedTrigger');
  });

  it('nói rõ phần nào không lấy được, không để AI đọc khoảng trống thành 0', () => {
    const text = buildAiPrompt(CONTENT, {
      ...FUNDAMENTALS, incomeStatements: [], businessPlan: null, indicators: null,
    } as CompanyFundamentals);

    expect(text).toContain('Chỉ số cơ bản: không lấy được');
    expect(text).toContain('Doanh thu theo quý: không lấy được');
  });

  it('panel chưa tải xong vẫn sao chép được phần hồ sơ', () => {
    const text = buildAiPrompt(CONTENT, null);

    expect(text).toContain('Cho thuê tài chính cho doanh nghiệp vừa và nhỏ.');
    expect(text).toContain('Không lấy được số liệu doanh nghiệp');
  });

  it('bỏ moat rỗng khỏi nội dung gửi đi', () => {
    const text = buildAiPrompt(CONTENT, FUNDAMENTALS);

    expect(text).toContain('- Chi phí vốn thấp nhờ cổ đông ngân hàng');
    expect(text).not.toContain('- \n');
  });
});

describe('parseAiPayload', () => {
  const wrap = (obj: unknown) => 'Đây là bản tôi soát lại:\n\n```json\n' + JSON.stringify(obj) + '\n```';

  it('đọc khối json và trả đúng các trường', () => {
    const result = parseAiPayload(
      wrap({
        symbol: 'EVF',
        businessModel: 'Mô hình mới',
        moats: [{ description: 'Moat A' }],
        riskFactors: [{ rank: 1, description: 'R1', observableSignal: 'S1', isDealBreaker: false, suggestedTrigger: 'TrendBreak' }],
        notes: 'ghi chú',
      }),
      'EVF',
    );

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.value.businessModel).toBe('Mô hình mới');
    expect(result.value.moats).toEqual([{ description: 'Moat A' }]);
    expect(result.value.riskFactors[0].suggestedTrigger).toBe('TrendBreak');
    expect(result.value.notes).toBe('ghi chú');
  });

  it('lấy khối json CUỐI CÙNG — AI hay nháp trước rồi mới chốt', () => {
    const text = '```json\n{"businessModel":"bản nháp"}\n```\nSửa lại:\n```json\n{"businessModel":"bản chốt"}\n```';
    const result = parseAiPayload(text, 'EVF');

    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value.businessModel).toBe('bản chốt');
  });

  it('không phải JSON thì báo lỗi, không trả dữ liệu nửa vời', () => {
    const result = parseAiPayload('Tôi nghĩ hồ sơ này ổn rồi, không cần sửa gì.', 'EVF');

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain('Không tìm thấy JSON');
  });

  it('JSON hỏng cú pháp thì báo lỗi riêng', () => {
    const result = parseAiPayload('```json\n{"businessModel": }\n```', 'EVF');

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain('không hợp lệ');
  });

  // Dán nội dung mã khác rồi lưu và ký là lỗi im lặng — sau đó không ai bắt được nữa.
  it('CHẶN khi symbol khác mã đang mở', () => {
    const result = parseAiPayload(wrap({ symbol: 'HPG', businessModel: 'thép' }), 'EVF');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain('HPG');
      expect(result.error).toContain('EVF');
    }
  });

  it('symbol khác hoa/thường vẫn nhận', () => {
    expect(parseAiPayload(wrap({ symbol: ' evf ', businessModel: 'x' }), 'EVF').ok).toBe(true);
  });

  it('không có symbol thì vẫn nhận — chỉ chặn khi nói rõ mã khác', () => {
    expect(parseAiPayload(wrap({ businessModel: 'x' }), 'EVF').ok).toBe(true);
  });

  it('đánh lại rank 1..N khi AI trả trùng hoặc thiếu', () => {
    const result = parseAiPayload(
      wrap({ riskFactors: [{ rank: 5, description: 'A' }, { rank: 5, description: 'B' }, { description: 'C' }] }),
      'EVF',
    );

    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value.riskFactors.map((r) => r.rank)).toEqual([1, 2, 3]);
  });

  it('chỉ giữ yếu tố hủy diệt đầu tiên và nói ra là đã bỏ bớt', () => {
    const result = parseAiPayload(
      wrap({ riskFactors: [{ description: 'A', isDealBreaker: true }, { description: 'B', isDealBreaker: true }] }),
      'EVF',
    );

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.value.riskFactors.map((r) => r.isDealBreaker)).toEqual([true, false]);
    expect(result.warnings.join(' ')).toContain('hủy diệt');
  });

  it('trigger lạ về null và được cảnh báo, không lặng lẽ nuốt', () => {
    const result = parseAiPayload(wrap({ riskFactors: [{ description: 'A', suggestedTrigger: 'MoonPhase' }] }), 'EVF');

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.value.riskFactors[0].suggestedTrigger).toBeNull();
    expect(result.warnings.join(' ')).toContain('kịch bản');
  });

  it('observableSignal rỗng vẫn nhận — để validation của form bắt, buộc người dùng tự điền', () => {
    const result = parseAiPayload(wrap({ riskFactors: [{ description: 'A' }] }), 'EVF');

    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value.riskFactors[0].observableSignal).toBe('');
  });

  it('bỏ qua trường lạ thay vì vỡ', () => {
    const result = parseAiPayload(wrap({ businessModel: 'x', confidenceScore: 0.9, confirmedAt: '2026-01-01' }), 'EVF');

    expect(result.ok).toBe(true);
    if (result.ok) expect(Object.keys(result.value)).toEqual(['businessModel', 'moats', 'riskFactors', 'notes']);
  });

  it('JSON trần không có hàng rào ``` vẫn đọc được', () => {
    expect(parseAiPayload('{"businessModel":"trần"}', 'EVF').ok).toBe(true);
  });

  // AI trích một đoạn code trong chính nội dung hồ sơ thì dấu ``` lọt vào giữa, cắt hàng rào sai chỗ.
  it('đọc được khi nội dung JSON có chứa dấu hàng rào bên trong chuỗi', () => {
    const text = '```json\n' + JSON.stringify({
      businessModel: 'Bán phần mềm; tài liệu có đoạn ``` ví dụ ``` trong đó.',
      notes: 'x',
    }) + '\n```';

    const result = parseAiPayload(text, 'EVF');

    expect(result.ok).toBe(true);
    if (result.ok) expect(result.value.notes).toBe('x');
  });

  it('mảng thay vì object thì từ chối', () => {
    expect(parseAiPayload('[{"businessModel":"x"}]', 'EVF').ok).toBe(false);
  });
});
