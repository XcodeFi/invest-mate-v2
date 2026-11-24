import { TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { TaGroup } from './ta-group-layers';

/** Một chỉ báo trong phần "Cách đọc": viết cho người mới, câu đời thường, có ví dụ số, không thuật ngữ chưa giải thích. */
export interface GuideItem {
  name: string;
  /** Là gì (1 câu). */
  what: string;
  /** Nhìn trên biểu đồ: màu, vị trí. */
  onChart: string;
  /** Cách đọc (2–4 gạch đầu dòng). */
  howToRead: string[];
  /** Bẫy hay gặp (1 câu). */
  pitfall: string;
}

export interface GroupGuide {
  title: string;
  oneLine: string;
  items: GuideItem[];
}

/** Nội dung phải khớp mục tương ứng trong frontend/src/assets/docs/phan-tich-thi-truong.md (Help). */
export const INDICATOR_GUIDES: Record<TaGroup, GroupGuide> = {
  'tong-hop': {
    title: 'Tổng hợp',
    oneLine: 'Gom các chỉ báo lại thành một con số và một gợi ý giá vào, cắt lỗ, chốt lời để bạn tham khảo.',
    items: [
      {
        name: 'Điểm Confluence',
        what: 'Điểm từ 0 đến 100 cho biết có bao nhiêu chỉ báo cùng nghiêng về một phía (mua hoặc bán).',
        onChart: 'Là con số ở thẻ Tổng hợp phía dưới biểu đồ, không vẽ lên biểu đồ.',
        howToRead: [
          'Trên 60: đa số chỉ báo nghiêng về mua. Dưới 40: đa số nghiêng về bán. Từ 40 đến 60: các chỉ báo chưa đồng thuận.',
          'Ví dụ: 72 điểm nghĩa là nhiều chỉ báo đang cùng chiều, độ tin cậy cao hơn khi chỉ có một chỉ báo báo mua.',
        ],
        pitfall: 'Điểm cao chỉ nói các chỉ báo đồng thuận trên dữ liệu quá khứ, không đảm bảo giá sẽ đi đúng hướng.',
      },
      {
        name: 'Gợi ý Giá vào, Cắt lỗ, Chốt lời',
        what: 'Ba mức giá hệ thống đề xuất dựa trên hỗ trợ, kháng cự và biến động gần đây.',
        onChart: 'Ba đường ngang đứt nét: xanh dương là giá vào, đỏ là cắt lỗ, xanh lá là chốt lời; mỗi đường có nhãn và giá ở chú giải bên dưới.',
        howToRead: [
          'Cắt lỗ là mức giá nếu chạm thì nhận sai và thoát, để lỗ không vượt quá mức bạn chấp nhận.',
          'Chốt lời nằm xa giá vào hơn cắt lỗ thì mới đáng vào: ví dụ vào 100, cắt lỗ 95 (rủi ro 5), chốt lời 110 (lãi 10) là lãi gấp đôi rủi ro.',
        ],
        pitfall: 'Đây là gợi ý để bạn so với kế hoạch của mình, không phải lệnh phải làm theo.',
      },
    ],
  },

  'xu-huong': {
    title: 'Xu hướng',
    oneLine: 'Trả lời: giá đang đi lên, đi xuống hay đi ngang, và xu hướng đó mạnh hay yếu.',
    items: [
      {
        name: 'EMA 20 và EMA 50 (đường trung bình)',
        what: 'Đường trung bình của giá đóng cửa 20 và 50 phiên gần nhất, trong đó phiên gần đây được tính nặng hơn. Nó làm mượt biến động hằng ngày để thấy hướng đi chung.',
        onChart: 'Hai đường đè lên nến: EMA 20 màu xanh dương (nhanh hơn), EMA 50 màu cam (chậm hơn).',
        howToRead: [
          'EMA 20 nằm trên EMA 50 và cả hai dốc lên: xu hướng ngắn hạn đang tăng. Ngược lại là đang giảm.',
          'Giá nằm trên cả hai đường: phe mua đang giữ thế. Giá chui xuống dưới cả hai: thế đã yếu đi.',
          'Hai đường cắt nhau là lúc xu hướng có thể đổi chiều.',
        ],
        pitfall: 'Khi giá đi ngang, hai đường cắt nhau liên tục và cho tín hiệu giả; hãy xem thêm ADX bên dưới trước khi tin.',
      },
      {
        name: 'ADX 14, +DI và −DI',
        what: 'ADX đo xu hướng mạnh hay yếu (không cho biết hướng). +DI là lực của phe mua, −DI là lực của phe bán.',
        onChart: 'Một tầng riêng bên dưới: ADX màu tím, +DI màu xanh lá, −DI màu đỏ, kèm vạch mờ ở mức 25 ("Có xu hướng").',
        howToRead: [
          'ADX từ 25 trở lên: có xu hướng rõ. Dưới 25: giá đang đi ngang hoặc chưa rõ hướng, ví dụ ADX 18.',
          '+DI nằm trên −DI: lực tăng trội hơn. −DI nằm trên +DI: lực giảm trội hơn.',
          'ADX càng cao thì xu hướng càng mạnh, nhưng ADX cao vẫn có thể là xu hướng giảm: nhìn +DI và −DI để biết hướng.',
        ],
        pitfall: 'ADX là chỉ báo chậm: lên cao thường đã đi được một đoạn, và ADX giảm không có nghĩa là giá đảo chiều ngay.',
      },
    ],
  },

  'dong-luong': {
    title: 'Động lượng',
    oneLine: 'Trả lời: đà đi của giá còn mạnh không, có đang quá nóng hay quá lạnh, sắp đổi chiều chưa.',
    items: [
      {
        name: 'RSI 14',
        what: 'Con số từ 0 đến 100 so sánh lực tăng và lực giảm của 14 phiên gần nhất. Cao nghĩa là giá vừa tăng mạnh, thấp nghĩa là vừa giảm mạnh.',
        onChart: 'Một tầng riêng, đường tím dao động giữa hai vạch mờ: 70 "Quá mua" ở trên, 30 "Quá bán" ở dưới.',
        howToRead: [
          'Trên 70: giá đã tăng nóng, dễ điều chỉnh. Dưới 30: giá đã giảm sâu, dễ hồi. Ví dụ RSI 72 là vùng nóng.',
          'Từ 30 đến 70: chưa quá nóng cũng chưa quá lạnh, không có tín hiệu riêng.',
          'Nên chờ RSI quay đầu khỏi vùng nóng/lạnh rồi mới coi là tín hiệu, không chỉ vì vừa chạm 70 hay 30.',
        ],
        pitfall: 'Trong xu hướng tăng mạnh RSI có thể ở trên 70 rất lâu; "quá mua" không có nghĩa là phải bán ngay.',
      },
      {
        name: 'MACD 12 26 9',
        what: 'So sánh hai đường trung bình (nhanh và chậm) để thấy đà tăng hay giảm đang mạnh lên hay yếu đi.',
        onChart: 'Một tầng riêng: đường MACD màu xanh dương, đường tín hiệu (Signal) màu cam, và các cột xám (Histogram) là khoảng cách giữa hai đường.',
        howToRead: [
          'MACD cắt lên trên đường tín hiệu: đà tăng đang lên. Cắt xuống dưới: đà giảm đang lên.',
          'Cột Histogram cao dần: đà đang mạnh lên; thấp dần về 0: đà đang yếu đi.',
          'MACD trên mức 0 nghĩa là xu hướng chung nghiêng về tăng, dưới 0 là nghiêng về giảm.',
        ],
        pitfall: 'MACD phản ứng chậm hơn giá và hay cho tín hiệu giả khi giá đi ngang.',
      },
      {
        name: 'Stochastic 14 3',
        what: 'Cho biết giá đóng cửa đang nằm ở đâu trong biên độ cao-thấp của 14 phiên gần nhất, từ 0 (sát đáy) đến 100 (sát đỉnh).',
        onChart: 'Một tầng riêng: %K màu xanh dương (nhanh), %D màu cam (chậm, là trung bình của %K), hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".',
        howToRead: [
          'Trên 80: giá đang sát đỉnh biên độ gần đây. Dưới 20: giá đang sát đáy.',
          '%K cắt lên trên %D ở vùng dưới 20 thường được xem là tín hiệu hồi; cắt xuống ở vùng trên 80 là tín hiệu yếu đi.',
        ],
        pitfall: 'Rất nhạy và hay đổi chiều liên tục, nên chỉ dùng để xác nhận cùng RSI hoặc xu hướng, không dùng một mình.',
      },
    ],
  },

  'bien-dong': {
    title: 'Biến động',
    oneLine: 'Trả lời: giá thường dao động rộng cỡ nào, để đặt cắt lỗ đủ xa và biết lúc nào giá đang đi quá đà.',
    items: [
      {
        name: 'Bollinger 20 2',
        what: 'Một dải gồm đường trung bình 20 phiên ở giữa, cùng hai đường cách đều hai bên (2 lần độ lệch chuẩn). Phần lớn thời gian giá nằm trong dải.',
        onChart: 'Ba đường màu xám đè lên nến: đường giữa đậm hơn, hai đường ngoài nhạt hơn (trên và dưới).',
        howToRead: [
          'Dải hẹp lại: giá đang đi hẹp, thường báo trước một nhịp chạy mạnh (chưa biết lên hay xuống).',
          'Dải mở rộng: biến động đang lớn.',
          'Giá chạm hoặc vượt đường trên: đang đi xa mức trung bình, dễ chững lại hoặc quay về giữa dải. Đường dưới thì ngược lại.',
        ],
        pitfall: 'Giá bám dọc đường trên khi xu hướng tăng mạnh là bình thường; chạm đường trên không tự động là tín hiệu bán.',
      },
      {
        name: 'ATR 14',
        what: 'Biên độ dao động trung bình mỗi phiên trong 14 phiên gần nhất, tính bằng đồng (không cho biết hướng).',
        onChart: 'Một tầng riêng, đường màu xanh ngọc; số càng lớn nghĩa là giá càng dao động mạnh.',
        howToRead: [
          'Ví dụ ATR là 2.000đ: mỗi phiên giá thường chạy cỡ 2.000đ, nên cắt lỗ sát hơn mức đó dễ bị quét nhầm.',
          'Cắt lỗ thường đặt cách giá vào khoảng 1,5 đến 2 lần ATR.',
          'ATR tăng nhanh: thị trường đang biến động hơn, nên giảm khối lượng mua.',
        ],
        pitfall: 'ATR chỉ nói dao động rộng bao nhiêu, không nói giá sẽ đi lên hay xuống.',
      },
    ],
  },

  'dong-tien': {
    title: 'Dòng tiền',
    oneLine: 'Trả lời: tiền vào mua có thật không, hay giá chỉ nhích lên khi ít người giao dịch.',
    items: [
      {
        name: 'Khối lượng và KL SMA 20',
        what: 'Số cổ phiếu khớp mỗi phiên. KL SMA 20 là khối lượng trung bình 20 phiên gần nhất, dùng làm mốc so sánh.',
        onChart: 'Một tầng riêng: các cột xám là khối lượng từng phiên, đường màu cam là trung bình 20 phiên.',
        howToRead: [
          'Cột cao hơn hẳn đường cam (ví dụ gấp 2 lần): phiên đó có nhiều người quan tâm.',
          'Giá tăng kèm khối lượng cao: đà tăng có nhiều tiền ủng hộ. Giá tăng nhưng khối lượng thấp: đà tăng yếu, dễ không bền.',
        ],
        pitfall: 'Khối lượng đột biến vào ngày đáo hạn, cơ cấu chỉ số hoặc tin lớn có thể không phản ánh dòng tiền bình thường.',
      },
      {
        name: 'OBV',
        what: 'Cộng dồn khối lượng: phiên giá tăng thì cộng khối lượng vào, phiên giá giảm thì trừ đi. Cho thấy dòng tiền tích luỹ đi vào hay đi ra.',
        onChart: 'Một tầng riêng, đường màu xanh dương; điều quan trọng là hướng đi, không phải con số tuyệt đối.',
        howToRead: [
          'OBV đi lên cùng giá: dòng tiền ủng hộ xu hướng tăng.',
          'Giá tăng nhưng OBV đi ngang hoặc giảm: tiền không đi theo, đà tăng có thể yếu.',
        ],
        pitfall: 'Con số OBV phụ thuộc điểm bắt đầu tính nên không so sánh giữa các mã, chỉ so hướng đi của từng mã.',
      },
      {
        name: 'MFI 14',
        what: 'Giống RSI nhưng có tính cả khối lượng, từ 0 đến 100: cao nghĩa là tiền đang đổ vào mạnh, thấp nghĩa là tiền đang rút ra.',
        onChart: 'Một tầng riêng, đường màu tím, hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".',
        howToRead: [
          'Trên 80: tiền vào đã rất mạnh, dễ quá nóng. Dưới 20: tiền rút mạnh, dễ có nhịp hồi.',
          'MFI quay đầu khỏi vùng 80 hoặc 20 là lúc đáng chú ý, giống cách đọc RSI.',
        ],
        pitfall: 'Cũng như RSI, có thể ở vùng trên 80 rất lâu khi xu hướng tăng mạnh.',
      },
    ],
  },

  'moc-gia': {
    title: 'Mốc giá',
    oneLine: 'Trả lời: nên vào hoặc ra quanh vùng giá nào, và giá có thể gặp cản ở đâu.',
    items: [
      {
        name: 'Hỗ trợ và kháng cự',
        what: 'Hỗ trợ là vùng giá bên dưới nơi giá từng dừng giảm và bật lên; kháng cự là vùng giá bên trên nơi giá từng dừng tăng và quay xuống.',
        onChart: 'Đường ngang đứt nét: xanh lá là hỗ trợ, đỏ là kháng cự, mỗi đường có nhãn (Hỗ trợ 1, Kháng cự 1...) và giá ở chú giải bên dưới.',
        howToRead: [
          'Giá đang tiến sát hỗ trợ: có thể bật lên, và là chỗ tham khảo để đặt cắt lỗ ngay bên dưới.',
          'Giá đang tiến sát kháng cự: có thể gặp cản, là chỗ tham khảo để chốt lời một phần.',
          'Giá vượt hẳn kháng cự với khối lượng lớn thì kháng cự đó thường trở thành hỗ trợ mới.',
        ],
        pitfall: 'Mốc giá là vùng chứ không phải một con số chính xác; giá có thể xuyên qua rồi quay lại.',
      },
      {
        name: 'Fibonacci (đường thoái lui)',
        what: 'Các mức cắt đoạn đi từ đáy đến đỉnh gần nhất theo tỉ lệ 23,6%, 38,2%, 50%, 61,8%, 78,6%, thường là chỗ giá chững lại khi điều chỉnh.',
        onChart: 'Các đường ngang đứt nét màu cam có nhãn Fib và giá, cùng hai đường xám là Đỉnh swing và Đáy swing.',
        howToRead: [
          'Giá đang điều chỉnh sau nhịp tăng: các mức 38,2%, 50%, 61,8% là chỗ thường được chú ý để canh mua lại.',
          'Giá giữ được mức 61,8% rồi bật lên là dấu hiệu nhịp điều chỉnh còn khỏe; thủng hẳn mức 78,6% là điều chỉnh sâu.',
        ],
        pitfall: 'Các mức này không có gì huyền bí: càng nhiều người cùng nhìn thì càng có tác dụng, nên chỉ dùng khi trùng với hỗ trợ hoặc kháng cự khác.',
      },
    ],
  },
};

// ---------- Đọc nhanh hiện tại ----------

const lastOf = (values: (number | null | undefined)[] | undefined): number | null => {
  if (!values || values.length === 0) return null;
  const v = values[values.length - 1];
  return v === null || v === undefined ? null : v;
};

const vi = (v: number, digits = 0, minDigits = 0): string =>
  v.toLocaleString('vi-VN', { minimumFractionDigits: minDigits, maximumFractionDigits: digits });

const price = (v: number): string => v.toLocaleString('vi-VN', { maximumFractionDigits: Math.abs(v) >= 1000 ? 0 : 2 });

/**
 * Đọc giá trị phiên cuối của nhóm thành câu dễ hiểu. Hàm thuần. Thiếu dữ liệu thì bỏ câu đó, không đoán.
 * Chỉ mô tả hiện trạng; không đưa ra khuyến nghị mua bán.
 */
export function readNow(group: TaGroup, s: TechnicalSeries, a: TechnicalAnalysis): string[] {
  const out: string[] = [];

  switch (group) {
    case 'tong-hop': {
      if (a.confluenceScore !== null && a.confluenceScore !== undefined) {
        const lean = a.confluenceScore > 60 ? 'nghiêng về mua' : a.confluenceScore < 40 ? 'nghiêng về bán' : 'chưa đồng thuận, trung tính';
        out.push(`Điểm Confluence ${vi(a.confluenceScore, 1, 1)}/100: các chỉ báo ${lean}.`);
      }
      if (a.suggestedEntry !== null && a.suggestedEntry !== undefined) {
        const parts = [`vào ${price(a.suggestedEntry)}`];
        if (a.suggestedStopLoss != null) parts.push(`cắt lỗ ${price(a.suggestedStopLoss)}`);
        if (a.suggestedTarget != null) parts.push(`chốt lời ${price(a.suggestedTarget)}`);
        out.push(`Gợi ý của hệ thống để tham khảo: ${parts.join(', ')}.`);
      }
      break;
    }

    case 'xu-huong': {
      const ema20 = lastOf(s.ema20), ema50 = lastOf(s.ema50);
      if (ema20 !== null && ema50 !== null) {
        if (ema20 > ema50) out.push('EMA20 trên EMA50: xu hướng ngắn hạn đang tăng.');
        else if (ema20 < ema50) out.push('EMA20 dưới EMA50: xu hướng ngắn hạn đang giảm.');
        else out.push('EMA20 bằng EMA50: xu hướng ngắn hạn chưa rõ.');
      }
      const adx = lastOf(s.adx);
      if (adx !== null) {
        out.push(adx >= 25
          ? `ADX ${vi(adx)}, từ 25 trở lên: xu hướng đã rõ.`
          : `ADX ${vi(adx)}, dưới 25: chưa có xu hướng rõ.`);
      }
      const plus = lastOf(s.plusDi), minus = lastOf(s.minusDi);
      if (plus !== null && minus !== null && plus !== minus) {
        out.push(plus > minus ? '+DI trên −DI: lực tăng đang trội hơn lực giảm.' : '−DI trên +DI: lực giảm đang trội hơn lực tăng.');
      }
      break;
    }

    case 'dong-luong': {
      const rsi = lastOf(s.rsi14);
      if (rsi !== null) {
        if (rsi > 70) out.push(`RSI đang ${vi(rsi)}, trên 70: giá đã tăng nóng, dễ điều chỉnh.`);
        else if (rsi < 30) out.push(`RSI đang ${vi(rsi)}, dưới 30: giá đã giảm sâu, dễ có nhịp hồi.`);
        else out.push(`RSI đang ${vi(rsi)}, nằm giữa 30 và 70: chưa quá nóng cũng chưa quá lạnh.`);
      }
      const macd = lastOf(s.macdLine), signal = lastOf(s.macdSignal);
      if (macd !== null && signal !== null && macd !== signal) {
        out.push(macd > signal
          ? 'MACD đang trên đường tín hiệu: động lượng nghiêng về tăng.'
          : 'MACD đang dưới đường tín hiệu: động lượng nghiêng về giảm.');
      }
      const k = lastOf(s.stochK);
      if (k !== null) {
        if (k > 80) out.push(`Stochastic %K ${vi(k)}, trên 80: giá đang sát đỉnh biên độ 14 phiên gần nhất.`);
        else if (k < 20) out.push(`Stochastic %K ${vi(k)}, dưới 20: giá đang sát đáy biên độ 14 phiên gần nhất.`);
        else out.push(`Stochastic %K ${vi(k)}, nằm giữa 20 và 80: giá ở khoảng giữa biên độ 14 phiên gần nhất.`);
      }
      break;
    }

    case 'bien-dong': {
      const close = lastOf(s.close), upper = lastOf(s.bollingerUpper), lower = lastOf(s.bollingerLower);
      if (close !== null && upper !== null && lower !== null) {
        if (close > upper) out.push('Giá đóng cửa trên dải Bollinger trên: giá đang đi xa mức trung bình, dễ chững lại hoặc quay về.');
        else if (close < lower) out.push('Giá đóng cửa dưới dải Bollinger dưới: giá đang đi xa mức trung bình về phía giảm, dễ chững lại hoặc hồi lên.');
        else out.push('Giá đóng cửa nằm trong dải Bollinger: biến động ở mức bình thường.');
      }
      const atr = lastOf(s.atr14);
      if (atr !== null && close !== null && close > 0) {
        out.push(`ATR 14 khoảng ${price(atr)}, tức ${vi(atr / close * 100, 1, 1)}% giá: mỗi phiên giá thường chạy cỡ này, nên đặt cắt lỗ rộng hơn mức đó.`);
      }
      break;
    }

    case 'dong-tien': {
      const volume = lastOf(s.volume), avg = lastOf(s.volumeSma20);
      if (volume !== null && avg !== null && avg > 0) {
        // làm tròn trước khi chia ngưỡng, để câu chữ khớp con số hiển thị (1,49 hiện 1,5 thì phải nói "gấp")
        const ratio = Math.round(volume / avg * 10) / 10;
        if (ratio >= 1.5) out.push(`Khối lượng phiên cuối gấp ${vi(ratio, 1)} lần trung bình 20 phiên.`);
        else if (ratio < 0.7) out.push(`Khối lượng phiên cuối chỉ bằng ${vi(ratio, 1)} lần trung bình 20 phiên: thấp, tín hiệu giá kém tin cậy.`);
        else out.push(`Khối lượng phiên cuối khoảng ${vi(ratio, 1)} lần trung bình 20 phiên: mức bình thường.`);
      }
      const obv = s.obv;
      const obvNow = lastOf(obv);
      const obvBefore = obv && obv.length > 10 ? obv[obv.length - 11] : null;
      if (obvNow !== null && obvBefore !== null && obvBefore !== undefined && obvNow !== obvBefore) {
        out.push(obvNow > obvBefore
          ? 'OBV đang đi lên so với 10 phiên trước: dòng tiền vào đang ủng hộ giá.'
          : 'OBV đang đi xuống so với 10 phiên trước: dòng tiền đang rút ra.');
      }
      const mfi = lastOf(s.mfi14);
      if (mfi !== null) {
        if (mfi > 80) out.push(`MFI ${vi(mfi)}, trên 80: tiền vào đã rất mạnh, dễ quá nóng.`);
        else if (mfi < 20) out.push(`MFI ${vi(mfi)}, dưới 20: tiền rút mạnh, dễ có nhịp hồi.`);
        else out.push(`MFI ${vi(mfi)}, nằm giữa 20 và 80: dòng tiền ở mức bình thường.`);
      }
      break;
    }

    case 'moc-gia': {
      const current = a.currentPrice;
      if (current === null || current === undefined || current <= 0) break;
      const below = (a.supportLevels ?? []).filter(l => l < current);
      const above = (a.resistanceLevels ?? []).filter(l => l > current);
      if (below.length > 0) {
        const nearest = Math.max(...below);
        out.push(`Hỗ trợ gần nhất: ${price(nearest)}, thấp hơn giá hiện tại ${vi((current - nearest) / current * 100, 1, 1)}%.`);
      }
      if (above.length > 0) {
        const nearest = Math.min(...above);
        out.push(`Kháng cự gần nhất: ${price(nearest)}, cao hơn giá hiện tại ${vi((nearest - current) / current * 100, 1, 1)}%.`);
      }
      break;
    }
  }

  return out;
}
