import { INVALIDATION_TRIGGER_LABELS, RiskFactorDto } from '../../core/services/company-dossier.service';
import { CompanyFundamentals } from '../../core/services/market-data.service';

/**
 * Cầu nối với AI KHÔNG nối được MCP (ChatGPT web, Gemini…). AI nối MCP thì đã có
 * `upsert_company_dossier` rồi, nên ở đây cố ý KHÔNG đẻ format riêng: shape dán vào trùng đúng tham
 * số của tool đó, để cùng một prompt dùng được cả hai đường và chỉ có một hợp đồng phải giữ.
 */
export interface DossierContent {
  symbol: string;
  businessModel: string;
  moats: { description: string }[];
  riskFactors: RiskFactorDto[];
  notes: string | null;
}

export interface ParsedDossier {
  businessModel: string;
  moats: { description: string }[];
  riskFactors: RiskFactorDto[];
  notes: string | null;
}

export type ParseResult =
  | { ok: true; value: ParsedDossier; warnings: string[] }
  | { ok: false; error: string };

const TRIGGER_KEYS = Object.keys(INVALIDATION_TRIGGER_LABELS);

// --- Sao chép ---

export function buildAiPrompt(content: DossierContent, fundamentals: CompanyFundamentals | null): string {
  const parts = [
    `# Hồ sơ công ty: ${content.symbol}`,
    '',
    '## Doanh nghiệp này kiếm tiền bằng gì?',
    content.businessModel.trim() || '(chưa viết)',
    '',
    '## Lợi thế cạnh tranh (moat)',
    bulletList(content.moats.map((m) => m.description).filter((d) => d?.trim())),
    '',
    '## Yếu tố rủi ro (hạng 1 = nguy hiểm nhất)',
    riskSection(content.riskFactors),
    '',
    '## Ghi chú',
    content.notes?.trim() || '(không có)',
    '',
    '## Số liệu doanh nghiệp',
    fundamentalsSection(fundamentals),
    '',
    '---',
    '',
    instructions(content.symbol),
  ];
  return parts.join('\n');
}

function bulletList(items: string[]): string {
  return items.length ? items.map((i) => `- ${i}`).join('\n') : '(chưa có)';
}

function riskSection(risks: RiskFactorDto[]): string {
  if (!risks.length) return '(chưa có)';
  return risks
    .map((r) => {
      const flags = [
        r.isDealBreaker ? 'YẾU TỐ HỦY DIỆT' : null,
        r.suggestedTrigger ? `kịch bản: ${INVALIDATION_TRIGGER_LABELS[r.suggestedTrigger] ?? r.suggestedTrigger}` : null,
      ].filter(Boolean);
      const suffix = flags.length ? ` [${flags.join(' · ')}]` : '';
      return `${r.rank}. ${r.description || '(chưa mô tả)'}\n   - Dấu hiệu quan sát: ${r.observableSignal || '(chưa có)'}${suffix}`;
    })
    .join('\n');
}

/**
 * Phần nào provider không lấy được thì nói THẲNG là không lấy được. Bỏ trống hoặc điền 0 vào đây là
 * mời AI kết luận sai về doanh nghiệp rồi trả ngược một hồ sơ bịa.
 */
function fundamentalsSection(f: CompanyFundamentals | null): string {
  if (!f) return 'Không lấy được số liệu doanh nghiệp — đừng suy ra con số nào từ khoảng trống này.';

  const lines: string[] = ['Nguồn: 24hmoney. Phần nào ghi "không lấy được" thì KHÔNG được coi là bằng 0.', ''];

  if (f.indicators) {
    const i = f.indicators;
    lines.push(`- Chỉ số: P/E ${n(i.pe)} · P/B ${n(i.pb)} · ROE ${n(i.roe)}% · ROA ${n(i.roa)}% · EPS ${n(i.eps)} · Beta ${n(i.beta)}`);
    lines.push(`- 52 tuần: đáy ${n(i.min52W)} — đỉnh ${n(i.max52W)}`);
    if (i.auditFirmName) lines.push(`- Kiểm toán: ${i.auditFirmName}${i.auditIsBig4 ? ' (Big4)' : ''}`);
  } else {
    lines.push('- Chỉ số cơ bản: không lấy được');
  }

  if (f.company) {
    const c = f.company;
    lines.push(`- Công ty: ${c.companyName ?? '(không có tên)'} · sàn ${c.exchange ?? '?'} · ngành ${c.industry ?? '?'}`);
    if (c.majorShareholders?.length) {
      lines.push(`- Cổ đông lớn: ${c.majorShareholders.map((s) => `${s.name ?? '(không rõ tên)'} ${n(s.percentage)}%`).join(', ')}`);
    }
  } else {
    lines.push('- Thông tin công ty: không lấy được');
  }

  if (f.incomeStatements?.length) {
    lines.push('- Doanh thu / LN sau thuế theo quý (tỷ VND):');
    lines.push(...f.incomeStatements.map((r) => `  - ${r.period ?? '(kỳ không rõ)'}: DT ${n(r.revenue)} · LNST ${n(r.netProfit)}`));
  } else {
    lines.push('- Doanh thu theo quý: không lấy được');
  }

  if (f.businessPlan) {
    const b = f.businessPlan;
    lines.push(`- Kế hoạch ${b.year}: doanh thu ${n(b.revenuePlan)} tỷ · lợi nhuận ${n(b.profitPlan)} tỷ · cổ tức ${n(b.dividendPlan)}%`);
  } else {
    lines.push('- Kế hoạch kinh doanh: không lấy được');
  }

  return lines.join('\n');
}

function n(v: number | null | undefined): string {
  return v == null ? 'không lấy được' : String(v);
}

function instructions(symbol: string): string {
  return [
    `Hãy soát lại hồ sơ trên cho mã ${symbol} và trả về BẢN ĐẦY ĐỦ đã sửa, trong đúng một khối \`\`\`json.`,
    '',
    'Ràng buộc bắt buộc:',
    '- Mỗi yếu tố rủi ro PHẢI có `observableSignal` — một dấu hiệu quan sát được, đo được, không phải cảm nhận.',
    '- Tối đa MỘT yếu tố được đặt `isDealBreaker: true`.',
    `- \`suggestedTrigger\` chỉ nhận một trong: ${TRIGGER_KEYS.join(', ')} — hoặc null.`,
    '- Không bịa số liệu cho phần ghi "không lấy được".',
    '',
    '```json',
    JSON.stringify(
      {
        symbol,
        businessModel: 'string',
        moats: [{ description: 'string' }],
        riskFactors: [
          { rank: 1, description: 'string', observableSignal: 'string', isDealBreaker: false, suggestedTrigger: null },
        ],
        notes: 'string hoặc null',
      },
      null,
      2,
    ),
    '```',
  ].join('\n');
}

// --- Dán ---

export function parseAiPayload(text: string, expectedSymbol: string): ParseResult {
  const raw = extractJson(text);
  if (raw === null) return { ok: false, error: 'Không tìm thấy JSON trong nội dung đã dán.' };

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return { ok: false, error: 'Khối JSON không hợp lệ — kiểm tra lại phần AI trả về.' };
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { ok: false, error: 'JSON phải là một object hồ sơ.' };
  }

  const obj = parsed as Record<string, unknown>;

  // Chặn cứng, không chỉ cảnh báo: dán nội dung mã khác rồi lưu và ký là lỗi không ai bắt được nữa.
  const symbol = typeof obj['symbol'] === 'string' ? obj['symbol'].trim().toUpperCase() : null;
  if (symbol && symbol !== expectedSymbol.trim().toUpperCase()) {
    return { ok: false, error: `Nội dung này của mã ${symbol}, trang đang mở là ${expectedSymbol}.` };
  }

  const warnings: string[] = [];
  const { riskFactors, riskWarnings } = normalizeRiskFactors(obj['riskFactors']);
  warnings.push(...riskWarnings);

  return {
    ok: true,
    warnings,
    value: {
      businessModel: typeof obj['businessModel'] === 'string' ? obj['businessModel'] : '',
      moats: normalizeMoats(obj['moats']),
      riskFactors,
      notes: typeof obj['notes'] === 'string' ? obj['notes'] : null,
    },
  };
}

/** Lấy khối ```json CUỐI CÙNG — AI hay giải thích trước rồi mới chốt bản cuối. */
function extractJson(text: string): string | null {
  const fences = [...text.matchAll(/```(?:json)?\s*([\s\S]*?)```/gi)];
  if (fences.length) return fences[fences.length - 1][1].trim();

  const trimmed = text.trim();
  return trimmed.startsWith('{') ? trimmed : null;
}

function normalizeMoats(value: unknown): { description: string }[] {
  if (!Array.isArray(value)) return [];
  return value
    .map((m) => {
      if (typeof m === 'string') return { description: m };
      const d = (m as Record<string, unknown>)?.['description'];
      return { description: typeof d === 'string' ? d : '' };
    })
    .filter((m) => m.description.trim());
}

function normalizeRiskFactors(value: unknown): { riskFactors: RiskFactorDto[]; riskWarnings: string[] } {
  const riskWarnings: string[] = [];
  if (!Array.isArray(value)) return { riskFactors: [], riskWarnings };

  let dealBreakerTaken = false;
  let droppedDealBreakers = 0;
  let unknownTriggers = 0;

  const riskFactors = value
    .filter((r) => r && typeof r === 'object')
    .map((r, index) => {
      const o = r as Record<string, unknown>;

      const wantsDealBreaker = o['isDealBreaker'] === true;
      let isDealBreaker = false;
      if (wantsDealBreaker && !dealBreakerTaken) {
        isDealBreaker = true;
        dealBreakerTaken = true;
      } else if (wantsDealBreaker) {
        droppedDealBreakers++;
      }

      const trigger = typeof o['suggestedTrigger'] === 'string' ? o['suggestedTrigger'] : null;
      const validTrigger = trigger && TRIGGER_KEYS.includes(trigger) ? trigger : null;
      if (trigger && !validTrigger) unknownTriggers++;

      // Rank đánh lại theo thứ tự mảng: AI hay trả trùng số hoặc bỏ trống, mà rank là thứ tự ưu tiên.
      return {
        rank: index + 1,
        description: typeof o['description'] === 'string' ? o['description'] : '',
        observableSignal: typeof o['observableSignal'] === 'string' ? o['observableSignal'] : '',
        isDealBreaker,
        suggestedTrigger: validTrigger,
      };
    });

  if (droppedDealBreakers > 0) {
    riskWarnings.push(`Bỏ bớt ${droppedDealBreakers} yếu tố hủy diệt — chỉ được giữ một.`);
  }
  if (unknownTriggers > 0) {
    riskWarnings.push(`${unknownTriggers} kịch bản vô hiệu hoá không hợp lệ đã bị bỏ trống.`);
  }

  return { riskFactors, riskWarnings };
}
