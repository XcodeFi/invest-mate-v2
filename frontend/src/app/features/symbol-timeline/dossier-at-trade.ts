import { vnDay } from '../../shared/indicators/vn-date';

/** Huy hiệu "hồ sơ lúc đó" do server tính (CompanyDossier.DescribeAtTrade); FE chỉ hiển thị, KHÔNG tự suy ra ngưỡng 90/180 ngày. */
export interface DossierAtTradeDto {
  state: string;                 // unsigned | resigned-after | signed
  daysSigned?: number | null;
  resignedAt?: string | null;
  freshness?: string | null;     // fresh | needs-review | expired (chỉ khi signed)
}

export interface DossierBadge {
  icon: string;
  text: string;
  tooltip: string;
  /** Lớp màu Tailwind cho chip. */
  cls: string;
}

export function dossierBadge(d: DossierAtTradeDto | null | undefined): DossierBadge | null {
  if (!d) return null;
  switch (d.state) {
    case 'unsigned':
      return { icon: '⚪', text: 'Hồ sơ chưa ký', tooltip: 'Mã này có hồ sơ công ty nhưng chưa được ký.', cls: 'bg-gray-100 text-gray-700' };
    case 'resigned-after': {
      const iso = d.resignedAt ? vnDay(d.resignedAt) : '';
      const day = iso ? `${iso.substring(8, 10)}/${iso.substring(5, 7)}` : '';
      return {
        icon: '🔁',
        text: `Hồ sơ ký lại sau lệnh này${day ? ` (${day})` : ''}`,
        tooltip: 'Hồ sơ được ký lại sau lệnh này; lần ký trước đó không được lưu nên không biết hồ sơ lúc đặt lệnh còn tươi hay đã cũ.',
        cls: 'bg-blue-50 text-blue-800'
      };
    }
    case 'signed': {
      const days = d.daysSigned ?? 0;
      let text = days === 0 ? 'Hồ sơ ký cùng ngày với lệnh' : `Hồ sơ ký ${days} ngày trước lệnh`;
      let icon = '✅';
      let cls = 'bg-green-50 text-green-800';
      if (d.freshness === 'needs-review') { text += ' · cần soát lại'; icon = '⚠️'; cls = 'bg-amber-50 text-amber-800'; }
      else if (d.freshness === 'expired') { text += ' · hết hạn'; icon = '⛔'; cls = 'bg-red-50 text-red-800'; }
      return { icon, text, tooltip: 'Tính từ lần ký hiện tại của hồ sơ tới ngày lệnh (theo ngày lịch giờ Việt Nam).', cls };
    }
    default:
      return null;
  }
}
