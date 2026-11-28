import { TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { TaGroup } from './ta-group-layers';

/** Một cách đọc: nói CÁCH đọc và LÝ DO đọc như vậy (mọi cách dùng đều phải có lý do). */
export interface HowToRead {
  how: string;
  why: string;
}

/**
 * Một nhà đầu tư/tác giả dùng chỉ báo này. `who` nêu rõ vai trò (tác giả chỉ báo hay người dùng), `source` là sách hoặc trang cụ thể,
 * `note` ghi chỗ cách dùng của họ KHÁC chỉ báo app đang vẽ (SMA/EMA, N 20 ngày/ATR 14...) và lưu ý riêng cho thị trường Việt Nam.
 */
export interface UsedBy {
  who: string;
  how: string;
  why: string;
  source: string;
  note?: string;
}

/** Một chỉ báo trong phần "Cách đọc": viết cho người mới, câu đời thường, có ví dụ số, không thuật ngữ chưa giải thích. */
export interface GuideItem {
  name: string;
  /** Là gì (1 câu). */
  what: string;
  /** Nhìn trên biểu đồ: màu, vị trí. */
  onChart: string;
  /** Cách đọc (2–4 mục), mỗi mục có lý do. */
  howToRead: HowToRead[];
  /** Nhà đầu tư lão luyện dùng thế nào, vì sao. Chỉ để trống khi chưa có ai được kiểm chứng; khi đó phải có `noUsers`. */
  usedBy: UsedBy[];
  /** Câu nói thẳng khi `usedBy` rỗng. */
  noUsers?: string;
  /** Bẫy hay gặp (1 câu). */
  pitfall: string;
}

export interface GroupGuide {
  title: string;
  oneLine: string;
  items: GuideItem[];
}

const NO_VERIFIED_USER = 'Chưa có nhà đầu tư nào có thành tích kiểm chứng được gắn với chỉ báo này; đây là phần ứng dụng tự tính.';

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
          { how: 'Trên 60: đa số chỉ báo nghiêng về mua. Dưới 40: đa số nghiêng về bán. Từ 40 đến 60: các chỉ báo chưa đồng thuận.',
            why: 'Một chỉ báo đơn lẻ hay báo giả; nhiều chỉ báo cùng chiều thì xác suất sai của từng cái giảm đi.' },
          { how: 'Ví dụ: 72 điểm nghĩa là nhiều chỉ báo đang cùng chiều, độ tin cậy cao hơn khi chỉ có một chỉ báo báo mua.',
            why: 'Các chỉ báo đều tính từ cùng một chuỗi giá nên đồng thuận chưa phải bằng chứng độc lập; vì vậy chỉ coi là tham khảo.' },
        ],
        usedBy: [],
        noUsers: NO_VERIFIED_USER,
        pitfall: 'Điểm cao chỉ nói các chỉ báo đồng thuận trên dữ liệu quá khứ, không đảm bảo giá sẽ đi đúng hướng.',
      },
      {
        name: 'Gợi ý Giá vào, Cắt lỗ, Chốt lời',
        what: 'Ba mức giá hệ thống đề xuất dựa trên hỗ trợ, kháng cự và biến động gần đây.',
        onChart: 'Ba đường ngang đứt nét: xanh dương là giá vào, đỏ là cắt lỗ, xanh lá là chốt lời; mỗi đường có nhãn và giá ở chú giải bên dưới.',
        howToRead: [
          { how: 'Cắt lỗ là mức giá nếu chạm thì nhận sai và thoát, để lỗ không vượt quá mức bạn chấp nhận.',
            why: 'Một lệnh sai không có điểm thoát định sẵn sẽ dễ bị giữ lại theo cảm xúc, và lỗ sâu thì phải lãi lớn hơn nhiều mới bù lại được.' },
          { how: 'Chốt lời nằm xa giá vào hơn cắt lỗ thì thường được xem là đáng cân nhắc: ví dụ vào 100, cắt lỗ 95 (rủi ro 5), chốt lời 110 (lãi 10) là lãi gấp đôi rủi ro.',
            why: 'Lãi gấp đôi rủi ro thì dù đúng ít hơn sai vẫn có thể có lãi; lãi nhỏ hơn rủi ro thì phải đúng rất nhiều mới hoà vốn.' },
        ],
        usedBy: [],
        noUsers: NO_VERIFIED_USER,
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
        onChart: 'Các đường đè lên nến: EMA 20 màu xanh dương (nhanh hơn), EMA 50 màu cam (chậm hơn), và khi đủ 200 phiên có thêm EMA 200 màu xanh ngọc (nền dài hạn).',
        howToRead: [
          { how: 'EMA 20 nằm trên EMA 50 và cả hai dốc lên: xu hướng ngắn hạn đang tăng. Ngược lại là đang giảm.',
            why: 'Đường trung bình dốc lên nghĩa là giá đóng cửa gần đây trung bình cao hơn trước; đường nhanh trên đường chậm cho thấy đà đang đi cùng chiều.' },
          { how: 'Giá nằm trên cả hai đường: phe mua đang giữ thế. Giá chui xuống dưới cả hai: thế đã yếu đi.',
            why: 'Người mua gần đây đang có lãi so với giá trung bình nên ít có lý do bán; khi giá xuống dưới, phần lớn người mua gần đây đang lỗ và dễ bán.' },
          { how: 'Hai đường cắt nhau là lúc xu hướng có thể đổi chiều.',
            why: 'Đường nhanh cắt đường chậm nghĩa là giá gần đây đã đi ngược hướng của giá trung bình dài hơn.' },
        ],
        usedBy: [
          {
            who: 'Mark Minervini (nhà đầu tư, vô địch US Investing Championship 1997)',
            how: 'Mẫu xu hướng (Trend Template): giá nằm trên các đường 150 và 200 ngày, đường 150 trên đường 200, đường 200 dốc lên ít nhất một tháng, đường 50 trên đường 150 và 200, giá trên đường 50.',
            why: 'Cổ phiếu tăng mạnh thường nằm trong giai đoạn tăng giá rõ ràng; bộ lọc này loại các cổ phiếu đang đi xuống hoặc đi ngang.',
            source: 'Mark Minervini, Trade Like a Stock Market Wizard (2013), nguồn thứ cấp về thành tích.',
            note: 'Ông dùng đường trung bình đơn giản (SMA) 50, 150, 200 ngày; ứng dụng vẽ EMA 20, 50, 200 nên không đối chiếu từng tiêu chí của ông được.',
          },
          {
            who: 'Paul Tudor Jones (nhà quản lý quỹ)',
            how: 'Chuyển sang phòng thủ khi giá nằm dưới đường trung bình 200 ngày của giá đóng cửa.',
            why: 'Tránh phải chịu những đợt sụt giảm lớn: bảo toàn vốn được ưu tiên hơn việc bắt đáy.',
            source: 'Phỏng vấn Paul Tudor Jones trong Tony Robbins, Money: Master the Game (2014), nguồn thứ cấp.',
            note: 'Ông dùng đường 200 ngày; ứng dụng vẽ EMA 200 khi mã có đủ 200 phiên. Các nghiên cứu về quy tắc đường trung bình cho kết quả trái chiều, nên dùng như bộ lọc rủi ro chứ không phải công cụ dự báo.',
          },
        ],
        pitfall: 'Khi giá đi ngang, hai đường cắt nhau liên tục và cho tín hiệu giả; hãy xem thêm ADX bên dưới trước khi tin.',
      },
      {
        name: 'ADX 14, +DI và −DI',
        what: 'ADX đo xu hướng mạnh hay yếu (không cho biết hướng). +DI là lực của phe mua, −DI là lực của phe bán.',
        onChart: 'Một tầng riêng bên dưới: ADX màu tím, +DI màu xanh lá, −DI màu đỏ, kèm vạch mờ ở mức 25 ("Có xu hướng").',
        howToRead: [
          { how: 'ADX từ 25 trở lên: có xu hướng rõ. Dưới 25: giá đang đi ngang hoặc chưa rõ hướng, ví dụ ADX 18.',
            why: 'Chiến lược theo xu hướng chỉ có lợi khi có xu hướng; khi đi ngang thì các tín hiệu cắt đường trung bình thường báo giả.' },
          { how: '+DI nằm trên −DI: lực tăng trội hơn. −DI nằm trên +DI: lực giảm trội hơn.',
            why: 'ADX không có hướng, nên cần so hai lực mua và bán để biết xu hướng mạnh đó đang đi lên hay đi xuống.' },
          { how: 'ADX càng cao thì xu hướng càng mạnh, nhưng ADX cao vẫn có thể là xu hướng giảm: nhìn +DI và −DI để biết hướng.',
            why: 'Xu hướng giảm mạnh cũng làm ADX tăng, vì ADX chỉ đo độ mạnh của cú đi một chiều.' },
        ],
        usedBy: [
          {
            who: 'J. Welles Wilder (tác giả chỉ báo)',
            how: 'Giới thiệu ADX và +DI, −DI trong hệ thống chuyển động có hướng; ADX trên 25 là có xu hướng mạnh, dưới 20 là gần như không có xu hướng.',
            why: 'Cần một thước đo tách bạch độ mạnh của xu hướng khỏi hướng của nó, để biết khi nào nên dùng chiến lược theo xu hướng.',
            source: 'J. Welles Wilder, New Concepts in Technical Trading Systems (1978); StockCharts ChartSchool: ADX.',
            note: 'Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư. Mức 25 là quy ước, vùng 20 đến 25 là vùng xám.',
          },
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
          { how: 'Trên 70: giá đã tăng nóng, dễ điều chỉnh. Dưới 30: giá đã giảm sâu, dễ hồi. Ví dụ RSI 72 là vùng nóng.',
            why: 'Sau một chuỗi phiên tăng liên tiếp, người mua đã có lãi và lực mua mới yếu dần, nên giá dễ chững lại; chiều giảm thì ngược lại.' },
          { how: 'Từ 30 đến 70: chưa quá nóng cũng chưa quá lạnh, không có tín hiệu riêng.',
            why: 'Ở vùng giữa, lực tăng và giảm chưa lệch nhiều nên con số không cho thêm thông tin ngoài xu hướng đang có.' },
          { how: 'Nên chờ RSI quay đầu khỏi vùng nóng/lạnh rồi mới coi là tín hiệu, không chỉ vì vừa chạm 70 hay 30.',
            why: 'Trong xu hướng mạnh RSI có thể nằm ở vùng nóng rất lâu; chỉ khi nó quay đầu mới cho thấy đà đã thật sự yếu đi.' },
        ],
        usedBy: [
          {
            who: 'J. Welles Wilder (tác giả chỉ báo)',
            how: 'Đưa ra RSI với chu kỳ 14 phiên để so mức tăng trung bình với mức giảm trung bình.',
            why: 'Cần một con số cố định trong khoảng 0 đến 100 để so độ nóng của các giai đoạn và các mã với nhau.',
            source: 'J. Welles Wilder, New Concepts in Technical Trading Systems (1978).',
            note: 'Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư. Ngưỡng 70 và 30 là quy ước phổ biến; dùng riêng nó, bằng chứng về hiệu quả còn hạn chế.',
          },
        ],
        pitfall: 'Trong xu hướng tăng mạnh RSI có thể ở trên 70 rất lâu; "quá mua" không có nghĩa là phải bán ngay.',
      },
      {
        name: 'MACD 12 26 9',
        what: 'So sánh hai đường trung bình (nhanh và chậm) để thấy đà tăng hay giảm đang mạnh lên hay yếu đi.',
        onChart: 'Một tầng riêng: đường MACD màu xanh dương, đường tín hiệu (Signal) màu cam, và các cột xám (Histogram) là khoảng cách giữa hai đường.',
        howToRead: [
          { how: 'MACD cắt lên trên đường tín hiệu: đà tăng đang lên. Cắt xuống dưới: đà giảm đang lên.',
            why: 'MACD là chênh lệch giữa đường trung bình nhanh và chậm; khi nó vượt đường làm mượt của chính nó, chênh lệch đang nở ra theo chiều đó.' },
          { how: 'Cột Histogram cao dần: đà đang mạnh lên; thấp dần về 0: đà đang yếu đi.',
            why: 'Histogram là khoảng cách giữa MACD và đường tín hiệu, nên nó co lại trước khi hai đường cắt nhau và báo sớm hơn.' },
          { how: 'MACD trên mức 0 nghĩa là xu hướng chung nghiêng về tăng, dưới 0 là nghiêng về giảm.',
            why: 'MACD bằng 0 nghĩa là đường nhanh bằng đường chậm; trên 0 nghĩa là giá gần đây đang cao hơn mức trung bình dài hơn.' },
        ],
        usedBy: [
          {
            who: 'Alexander Elder (nhà giao dịch, tác giả)',
            how: 'Hệ thống Ba màn hình: màn hình tuần (độ dốc của histogram MACD tuần hoặc EMA 13 tuần) cho biết "thuỷ triều"; màn hình ngày chờ chỉ báo dao động kéo ngược chiều thuỷ triều rồi mới vào lệnh; rủi ro tối đa 2% vốn mỗi lệnh, ngừng giao dịch trong tháng khi lỗ 6%.',
            why: 'Giao dịch thuận chiều xu hướng lớn thì thắng thế hơn, và giới hạn mức thiệt hại giữ cho một chuỗi sai không làm cạn vốn.',
            source: 'Alexander Elder, Trading for a Living (1993), nguồn thứ cấp.',
            note: 'Màn hình đầu của ông là khung tuần; ứng dụng chỉ vẽ nến ngày nên chỉ tham khảo được màn hình thứ hai.',
          },
        ],
        pitfall: 'MACD phản ứng chậm hơn giá và hay cho tín hiệu giả khi giá đi ngang.',
      },
      {
        name: 'Stochastic 14 3',
        what: 'Cho biết giá đóng cửa đang nằm ở đâu trong biên độ cao-thấp của 14 phiên gần nhất, từ 0 (sát đáy) đến 100 (sát đỉnh).',
        onChart: 'Một tầng riêng: %K màu xanh dương (nhanh), %D màu cam (chậm, là trung bình của %K), hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".',
        howToRead: [
          { how: 'Trên 80: giá đang sát đỉnh biên độ gần đây. Dưới 20: giá đang sát đáy.',
            why: 'Giá đóng cửa sát đỉnh biên độ nghĩa là người mua đang thắng thế đến phút cuối phiên; sát đáy thì ngược lại.' },
          { how: '%K cắt lên trên %D ở vùng dưới 20 thường được xem là tín hiệu hồi; cắt xuống ở vùng trên 80 là tín hiệu yếu đi.',
            why: '%D là đường làm mượt của %K nên %K vượt nó cho thấy đà ngắn hạn vừa đổi chiều, còn vùng 20 và 80 cho biết đổi chiều đó đang xảy ra ở vùng cực.' },
        ],
        usedBy: [
          {
            who: 'Alexander Elder (nhà giao dịch, tác giả)',
            how: 'Dùng chỉ báo dao động như Stochastic ở màn hình ngày của hệ thống Ba màn hình: mua khi nó rơi xuống vùng thấp trong lúc xu hướng tuần đi lên.',
            why: 'Chờ nhịp điều chỉnh ngắn trong xu hướng tăng để vào giá tốt hơn, thay vì đuổi theo giá.',
            source: 'Alexander Elder, Trading for a Living (1993), nguồn thứ cấp.',
            note: 'Ông chỉ dùng nó khi màn hình tuần đã xác nhận xu hướng; ứng dụng không vẽ khung tuần.',
          },
        ],
        pitfall: 'Rất nhạy và hay đổi chiều liên tục, thường chỉ được dùng để xác nhận cùng RSI hoặc xu hướng, không dùng một mình.',
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
          { how: 'Dải hẹp lại: giá đang đi hẹp, thường báo trước một nhịp chạy mạnh (chưa biết lên hay xuống).',
            why: 'Biến động có chu kỳ: sau giai đoạn yên lặng thường là giai đoạn biến động lớn, nhưng dải hẹp không cho biết hướng.' },
          { how: 'Dải mở rộng: biến động đang lớn.',
            why: 'Dải được tính từ độ lệch chuẩn của giá, nên giá dao động rộng hơn thì dải nở ra.' },
          { how: 'Giá chạm hoặc vượt đường trên: đang đi xa mức trung bình, dễ chững lại hoặc quay về giữa dải. Đường dưới thì ngược lại.',
            why: 'Hai đường ngoài cách mức trung bình 2 độ lệch chuẩn, nơi giá ít khi ở lâu; nhưng trong xu hướng mạnh giá có thể bám dọc đường ngoài.' },
        ],
        usedBy: [
          {
            who: 'John Bollinger (tác giả chỉ báo)',
            how: 'Giai đoạn nén: độ rộng dải (BandWidth) xuống thấp nhất trong khoảng 6 tháng thì biến động lớn thường theo sau; hướng đi được xác định khi giá phá ra khỏi dải; chạm dải trên một mình không phải tín hiệu bán.',
            why: 'Biến động lên xuống theo chu kỳ, nên lúc thị trường nén chặt nhất là lúc cần chuẩn bị cho nhịp mở rộng, còn hướng thì phải chờ giá cho biết.',
            source: 'John Bollinger, Bollinger on Bollinger Bands (2001); StockCharts ChartSchool: Bollinger BandWidth.',
            note: 'Cài đặt mặc định của ông là đường 20 phiên và 2 độ lệch chuẩn, giống ứng dụng. Ứng dụng chưa vẽ BandWidth nên chưa phát hiện được giai đoạn nén; đây là người tạo ra chỉ báo, không phải bằng chứng thành tích.',
          },
        ],
        pitfall: 'Giá bám dọc đường trên khi xu hướng tăng mạnh là bình thường; chạm đường trên không tự động là tín hiệu bán.',
      },
      {
        name: 'ATR 14',
        what: 'Biên độ dao động trung bình mỗi phiên trong 14 phiên gần nhất, tính bằng đồng (không cho biết hướng).',
        onChart: 'Một tầng riêng, đường màu xanh ngọc; số càng lớn nghĩa là giá càng dao động mạnh.',
        howToRead: [
          { how: 'Ví dụ ATR là 2.000đ: mỗi phiên giá thường chạy cỡ 2.000đ, cắt lỗ sát hơn mức đó dễ bị quét nhầm.',
            why: 'Cắt lỗ nằm trong biên độ dao động bình thường của một phiên sẽ bị chạm bởi nhiễu chứ không phải vì ý tưởng sai.' },
          { how: 'Cắt lỗ thường đặt cách giá vào khoảng 1,5 đến 2 lần ATR.',
            why: 'Cách xa vài lần biên độ thường ngày để chỉ những biến động bất thường mới chạm, đồng thời vẫn có mức thoát xác định.' },
          { how: 'ATR tăng nhanh: thị trường đang biến động hơn, nhiều người thường cân nhắc giảm khối lượng mua.',
            why: 'Cắt lỗ phải xa hơn thì cùng số tiền rủi ro chỉ mua được ít cổ phiếu hơn.' },
        ],
        usedBy: [
          {
            who: 'Các Turtle Traders của Richard Dennis và William Eckhardt',
            how: 'Dùng N (biên độ dao động trung bình thật) để cắt lỗ ở 2N và đặt đơn vị vị thế bằng 1% vốn chia cho N.',
            why: 'Mỗi vị thế chịu cùng một mức rủi ro bằng tiền dù cổ phiếu biến động mạnh hay yếu.',
            source: 'Curtis Faith, Way of the Turtle (2007).',
            note: 'N của họ là trung bình 20 ngày, ứng dụng dùng ATR 14 của Wilder, nên con số hơi khác. Ở Việt Nam, biên độ ±7% (HOSE) làm giá có thể nhảy cóc qua mức cắt lỗ; cổ phiếu mua về tài khoản vào chiều T+2 nên lỗ trong 2 phiên đầu chưa thoát được.',
          },
          {
            who: 'J. Welles Wilder (tác giả chỉ báo)',
            how: 'Tạo ra biên độ thật (true range) gồm cả khoảng trống giá giữa hai phiên, rồi lấy trung bình 14 phiên làm ATR.',
            why: 'Biên độ cao-thấp trong một phiên bỏ sót những cú nhảy cóc qua đêm, vốn là một phần rủi ro thật của người giữ cổ phiếu.',
            source: 'J. Welles Wilder, New Concepts in Technical Trading Systems (1978).',
            note: 'Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư.',
          },
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
          { how: 'Cột cao hơn hẳn đường cam (ví dụ gấp 2 lần): phiên đó có nhiều người quan tâm.',
            why: 'So với mức trung bình của chính mã đó mới biết phiên này bất thường hay không; khối lượng tuyệt đối khác nhau rất nhiều giữa các mã.' },
          { how: 'Giá tăng kèm khối lượng cao: đà tăng có nhiều tiền ủng hộ. Giá tăng nhưng khối lượng thấp: đà tăng yếu, dễ không bền.',
            why: 'Tổ chức lớn mua với khối lượng lớn nên để lại dấu vết trong khối lượng; giá nhích lên khi ít người mua thì dễ bị đảo ngược.' },
        ],
        usedBy: [
          {
            who: 'William O’Neil (nhà đầu tư, người sáng lập Investor’s Business Daily)',
            how: 'Mua khi giá phá điểm mua (pivot) với khối lượng cao hơn mức trung bình ít nhất 40 đến 50%, và cắt mọi lệnh lỗ ở mức 7 đến 8%.',
            why: 'Khối lượng cao khi phá điểm mua cho thấy các tổ chức đang mua thật; mức lỗ nhỏ cố định giữ cho tài khoản còn sống để làm lại.',
            source: 'William O’Neil, How to Make Money in Stocks (xuất bản lần đầu 1988, phương pháp CAN SLIM).',
            note: 'Ông so với khối lượng trung bình 50 ngày, ứng dụng dùng trung bình 20 ngày. Ở Việt Nam, biên độ ±7% (HOSE) có thể làm giá nhảy cóc qua mức cắt lỗ 7 đến 8%, và cổ phiếu mới mua chưa bán được trong 2 phiên đầu.',
          },
        ],
        pitfall: 'Khối lượng đột biến vào ngày đáo hạn, cơ cấu chỉ số hoặc tin lớn có thể không phản ánh dòng tiền bình thường.',
      },
      {
        name: 'OBV',
        what: 'Cộng dồn khối lượng: phiên giá tăng thì cộng khối lượng vào, phiên giá giảm thì trừ đi. Cho thấy dòng tiền tích luỹ đi vào hay đi ra.',
        onChart: 'Một tầng riêng, đường màu xanh dương; điều quan trọng là hướng đi, không phải con số tuyệt đối.',
        howToRead: [
          { how: 'OBV đi lên cùng giá: dòng tiền ủng hộ xu hướng tăng.',
            why: 'Giá tăng mà khối lượng cộng dồn cũng tăng nghĩa là các phiên tăng có nhiều giao dịch hơn các phiên giảm.' },
          { how: 'Giá tăng nhưng OBV đi ngang hoặc giảm: tiền không đi theo, đà tăng có thể yếu.',
            why: 'Nếu các phiên giảm có khối lượng lớn hơn các phiên tăng thì có người đang bán ra trong lúc giá nhích lên.' },
        ],
        usedBy: [
          {
            who: 'Joseph Granville (tác giả chỉ báo)',
            how: 'Khối lượng đi trước giá: OBV tăng trước khi giá tăng, nên theo dõi OBV để thấy dòng tiền sớm.',
            why: 'Dòng tiền lớn phải vào trước rồi giá mới phản ánh, nên khối lượng là dấu hiệu sớm hơn giá.',
            source: 'Joseph Granville, Granville’s New Key to Stock Market Profits (1963).',
            note: 'Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư.',
          },
        ],
        pitfall: 'Con số OBV phụ thuộc điểm bắt đầu tính nên không so sánh giữa các mã, chỉ so hướng đi của từng mã.',
      },
      {
        name: 'MFI 14',
        what: 'Giống RSI nhưng có tính cả khối lượng, từ 0 đến 100: cao nghĩa là tiền đang đổ vào mạnh, thấp nghĩa là tiền đang rút ra.',
        onChart: 'Một tầng riêng, đường màu tím, hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".',
        howToRead: [
          { how: 'Trên 80: tiền vào đã rất mạnh, dễ quá nóng. Dưới 20: tiền rút mạnh, dễ có nhịp hồi.',
            why: 'Cùng logic với RSI nhưng có thêm khối lượng, nên phản ánh cả sức nặng của các phiên tăng hay giảm.' },
          { how: 'MFI quay đầu khỏi vùng 80 hoặc 20 là lúc đáng chú ý, giống cách đọc RSI.',
            why: 'Ở vùng cực, chỉ khi con số quay đầu mới cho thấy đà đã đổi chiều; chạm vùng cực mà chưa quay thì chưa có thông tin mới.' },
        ],
        usedBy: [
          {
            who: 'Gene Quong và Avrum Soudack (tác giả chỉ báo)',
            how: 'Đưa khối lượng vào công thức kiểu RSI (RSI có trọng số khối lượng) để so tiền vào với tiền ra.',
            why: 'Một phiên tăng trên khối lượng rất lớn và một phiên tăng trên khối lượng rất nhỏ không có cùng ý nghĩa, nhưng RSI thường coi hai phiên như nhau.',
            source: 'Quong, G. và Soudack, A., "Volume-weighted RSI: money flow", Technical Analysis of Stocks & Commodities 7(3), 1989, tr. 76-77.',
            note: 'Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư.',
          },
        ],
        pitfall: 'Cũng như RSI, có thể ở vùng trên 80 rất lâu khi xu hướng tăng mạnh.',
      },
    ],
  },

  'moc-gia': {
    title: 'Mốc giá',
    oneLine: 'Trả lời: thường vào hoặc ra quanh vùng giá nào, và giá có thể gặp cản ở đâu.',
    items: [
      {
        name: 'Hỗ trợ và kháng cự',
        what: 'Hỗ trợ là vùng giá bên dưới nơi giá từng dừng giảm và bật lên; kháng cự là vùng giá bên trên nơi giá từng dừng tăng và quay xuống.',
        onChart: 'Đường ngang đứt nét: xanh lá là hỗ trợ, đỏ là kháng cự, mỗi đường có nhãn (Hỗ trợ 1, Kháng cự 1...) và giá ở chú giải bên dưới.',
        howToRead: [
          { how: 'Giá đang tiến sát hỗ trợ: có thể bật lên, và là chỗ tham khảo để đặt cắt lỗ ngay bên dưới.',
            why: 'Đó là vùng người mua từng xuất hiện; nếu giá thủng nó thì ý tưởng "sẽ bật lên" đã sai nên có lý do rõ ràng để thoát.' },
          { how: 'Giá đang tiến sát kháng cự: có thể gặp cản, là chỗ tham khảo để chốt lời một phần.',
            why: 'Đó là vùng người bán từng xuất hiện, và những người mua kẹt ở vùng này có thể bán khi giá quay lại hoà vốn.' },
          { how: 'Giá vượt hẳn kháng cự với khối lượng lớn thì kháng cự đó thường trở thành hỗ trợ mới.',
            why: 'Khối lượng lớn cho thấy phe mua đã hấp thụ hết lượng bán ở vùng đó, nên vùng giá ấy đổi vai.' },
        ],
        usedBy: [
          {
            who: 'Jesse Livermore (nhà đầu cơ)',
            how: 'Hành động ở các điểm then chốt (pivotal points): điểm đảo chiều khi một xu hướng mới bắt đầu và điểm tiếp diễn trong vùng tích luỹ của một xu hướng đang có; chờ giá vượt điểm đó mới vào.',
            why: 'Chờ giá xác nhận bằng cách đi theo hướng mình dự tính, thay vì đoán đỉnh đáy.',
            source: 'Jesse Livermore, How to Trade in Stocks (1940).',
            note: 'Ứng dụng chọn mốc theo đỉnh đáy gần nhất (swing), không phải theo định nghĩa điểm then chốt của ông.',
          },
          {
            who: 'Nicolas Darvas (vũ công, nhà đầu tư)',
            how: 'Lý thuyết hộp (box theory): coi giá đi trong các hộp; mép trên hộp là kháng cự, mép dưới là hỗ trợ.',
            why: 'Xem mốc giá như vùng mà thị trường đã chấp nhận, thay vì dựa vào dự đoán giá trị của cổ phiếu.',
            source: 'Nicolas Darvas, How I Made $2,000,000 in the Stock Market (1960).',
            note: 'Ứng dụng chưa tự dựng hộp Darvas; mốc hiển thị chỉ là hỗ trợ và kháng cự gần nhất.',
          },
        ],
        pitfall: 'Mốc giá là vùng chứ không phải một con số chính xác; giá có thể xuyên qua rồi quay lại.',
      },
      {
        name: 'Fibonacci (đường thoái lui)',
        what: 'Các mức cắt đoạn đi từ đáy đến đỉnh gần nhất theo tỉ lệ 23,6%, 38,2%, 50%, 61,8%, 78,6%, thường là chỗ giá chững lại khi điều chỉnh.',
        onChart: 'Các đường ngang đứt nét màu cam có nhãn Fib và giá, cùng hai đường xám là Đỉnh swing và Đáy swing.',
        howToRead: [
          { how: 'Giá đang điều chỉnh sau nhịp tăng: các mức 38,2%, 50%, 61,8% là chỗ thường được chú ý để cân nhắc mua lại.',
            why: 'Vì nhiều người cùng nhìn các mức này nên đôi khi có lệnh tập trung ở đó; bản thân tỉ lệ không có cơ sở thống kê vững.' },
          { how: 'Giá giữ được mức 61,8% rồi bật lên là dấu hiệu nhịp điều chỉnh còn khỏe; thủng hẳn mức 78,6% là điều chỉnh sâu.',
            why: 'Điều chỉnh càng sâu thì phần lớn nhịp tăng trước đã bị xoá, nên khả năng xu hướng cũ còn tiếp diễn càng giảm.' },
        ],
        usedBy: [],
        noUsers: 'Chưa có nhà đầu tư có thành tích kiểm chứng dùng làm quy tắc cốt lõi; bằng chứng yếu.',
        pitfall: 'Các mức này không có gì huyền bí: càng nhiều người cùng nhìn thì càng có tác dụng, thường chỉ được dùng khi trùng với hỗ trợ hoặc kháng cự khác.',
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
        else out.push('Giá đóng cửa nằm trong dải Bollinger: giá chưa đi xa mức trung bình.');
      }
      const atr = lastOf(s.atr14);
      if (atr !== null && close !== null && close > 0) {
        out.push(`ATR 14 khoảng ${price(atr)}, tức ${vi(atr / close * 100, 1, 1)}% giá: mỗi phiên giá thường chạy cỡ này, cắt lỗ hẹp hơn mức đó dễ bị quét nhầm.`);
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
