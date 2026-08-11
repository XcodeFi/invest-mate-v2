export type MoodKey = 'Calm' | 'Fomo' | 'Fear' | 'Revenge';

export interface Quote {
  text: string;
  /** Bỏ trống với câu ẩn dụ tự viết — không gán tên người thật cho câu không phải của họ. */
  author?: string;
}

export const MOOD_LABELS: Record<MoodKey, string> = {
  Calm: 'Bình tĩnh',
  Fomo: 'FOMO (sợ bỏ lỡ)',
  Fear: 'Sợ',
  Revenge: 'Cay cú',
};

export const QUOTES: Record<MoodKey, Quote[]> = {
  Calm: [
    { text: 'Thị trường chuyển tiền từ người sốt ruột sang người kiên nhẫn.', author: 'Warren Buffett' },
    { text: 'Tiền lớn không nằm ở chỗ mua bán, mà ở chỗ ngồi yên.', author: 'Charlie Munger' },
    { text: 'Trong ngắn hạn thị trường là cái máy bỏ phiếu; về dài hạn nó là cái cân.', author: 'Benjamin Graham' },
    { text: 'Phẩm chất quan trọng nhất của nhà đầu tư là tính khí, không phải trí tuệ.', author: 'Warren Buffett' },
    { text: 'Mặt hồ phẳng không phải vì không có gió. Vì đã đủ lâu không ai quăng đá.' },
    { text: 'Người câu giỏi không phải người quăng nhiều nhất.' },
  ],
  Fomo: [
    { text: 'Cá không chạy đi đâu. Người sốt ruột mới chạy.' },
    { text: 'Hãy sợ khi người khác tham, và tham khi người khác sợ.', author: 'Warren Buffett' },
    { text: 'Không phải suy nghĩ làm tôi kiếm được tiền lớn. Luôn luôn là việc ngồi yên.', author: 'Jesse Livermore' },
    { text: 'Chuyến tàu này anh lỡ. Ngày mai có chuyến khác. Tiền mất thì không có chuyến khác.' },
    { text: 'Quăng câu vì thấy người bên cạnh giật được cá — đó không phải là câu, đó là đuổi.' },
  ],
  Fear: [
    { text: 'Chìa khoá thật sự để kiếm tiền từ cổ phiếu là đừng để bị doạ ra khỏi chúng.', author: 'Peter Lynch' },
    { text: 'Kẻ thù lớn nhất của nhà đầu tư nhiều khả năng là chính anh ta.', author: 'Benjamin Graham' },
    { text: 'Giá là thứ bạn trả. Giá trị là thứ bạn nhận.', author: 'Warren Buffett' },
    { text: 'Nước động không có nghĩa là phải kéo cần lên.' },
    { text: 'Sợ thì ngồi im. Ngồi im không mất gì cả.' },
  ],
  Revenge: [
    { text: 'Tiền mất rồi không biết anh là ai. Nó không quay lại vì anh tức.' },
    { text: 'Quy tắc số 1: đừng để mất tiền. Quy tắc số 2: đừng quên quy tắc số 1.', author: 'Warren Buffett' },
    { text: 'Thị trường không nợ anh một lần gỡ.' },
    { text: 'Mất cá thì về. Mất cần câu thì hết câu.' },
    { text: 'Lệnh gỡ gạc là lệnh đắt nhất anh từng đặt.' },
    { text: 'Hôm nay không đặt lệnh nào cũng là một quyết định. Thường là quyết định đúng.' },
  ],
};

/**
 * Chọn câu theo `seed` (dùng khoá ngày `YYYY-MM-DD`) để cùng một ngày luôn ra cùng một câu —
 * đổi câu mỗi lần render thì không ai đọc hết được một câu nào.
 */
export function pickQuote(mood: MoodKey, seed: string): Quote {
  const pool = QUOTES[mood];
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) | 0;
  }
  return pool[Math.abs(hash) % pool.length];
}
