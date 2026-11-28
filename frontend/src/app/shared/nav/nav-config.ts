/**
 * Cấu hình MỘT NƠI cho menu điều hướng: menu trái (desktop) và menu điện thoại cùng đọc từ đây, không có bản sao nào khác.
 * Nhóm theo thời điểm làm việc. Mọi trang vào được bằng URL cố định đều có một mục; trang có tham số (`:symbol`, `:id`) không nằm đây.
 *
 * Thêm route mới thì PHẢI thêm một mục vào đây (hoặc vào NAV_EXCLUDED_ROUTES kèm lý do): test `nav-config.spec.ts` đối chiếu với `app.routes.ts`.
 * Route chỉ chuyển hướng (`redirectTo`, ví dụ /position-sizing và /advanced-analytics) KHÔNG có mục: test cũng chặn điều đó.
 */
export interface NavItem {
  path: string;
  label: string;
  icon: string;
}

export interface NavGroup {
  key: string;
  label: string;
  icon: string;
  items: NavItem[];
  /** Nhóm thu gọn sẵn (chỉ mở khi người dùng bấm, hoặc khi trang hiện tại nằm trong nhóm). */
  collapsedByDefault?: boolean;
}

export const NAV_GROUPS: NavGroup[] = [
  {
    key: 'today', label: 'HÔM NAY', icon: '🏠',
    items: [
      { path: '/dashboard', label: 'Tổng quan', icon: '📊' },
      { path: '/daily-routine', label: 'Nhiệm vụ ngày', icon: '📋' },
      { path: '/alerts', label: 'Cảnh báo', icon: '🔔' },
    ]
  },
  {
    key: 'opportunities', label: 'TÌM CƠ HỘI', icon: '🔭',
    items: [
      { path: '/market-data', label: 'Thị trường', icon: '🌐' },
      { path: '/screener', label: 'Sàng lọc', icon: '🔎' },
      { path: '/watchlist', label: 'Watchlist', icon: '⭐' },
      { path: '/company-dossier', label: 'Hồ sơ công ty', icon: '🏢' },
      { path: '/symbol-timeline', label: 'Dòng thời gian mã', icon: '🕒' },
    ]
  },
  {
    key: 'plan', label: 'KẾ HOẠCH & GD', icon: '📑',
    items: [
      { path: '/trade-plan', label: 'Kế hoạch GD', icon: '📑' },
      { path: '/trades', label: 'Giao dịch', icon: '🔄' },
      { path: '/positions', label: 'Vị thế', icon: '📌' },
      { path: '/trade-wizard', label: 'Wizard GD', icon: '🧙' },
      { path: '/strategies', label: 'Chiến lược', icon: '🎯' },
      { path: '/backtesting', label: 'Backtest', icon: '🧪' },
    ]
  },
  {
    key: 'review', label: 'ĐÁNH GIÁ', icon: '⚖️',
    items: [
      { path: '/pending-reviews', label: 'Chờ review', icon: '📝' },
      { path: '/campaign-analytics', label: 'Kỳ vọng & chiến dịch', icon: '🏆' },
      { path: '/risk-dashboard', label: 'Rủi ro (bảng)', icon: '🛡️' },
      { path: '/risk', label: 'Rủi ro chi tiết', icon: '⚠️' },
      { path: '/monthly-review', label: 'Báo cáo tháng', icon: '📅' },
      { path: '/analytics', label: 'Phân tích', icon: '📈' },
      { path: '/journals', label: 'Nhật ký', icon: '📓' },
      { path: '/snapshots', label: 'Lịch sử snapshot', icon: '📸' },
    ]
  },
  {
    key: 'money', label: 'TIỀN & TÀI SẢN', icon: '💼',
    items: [
      { path: '/portfolios', label: 'Danh mục', icon: '📁' },
      { path: '/capital-flows', label: 'Dòng vốn', icon: '💸' },
      { path: '/corporate-actions', label: 'Sự kiện quyền', icon: '🎁' },
      { path: '/personal-finance', label: 'Tài chính CN', icon: '💰' },
    ]
  },
  {
    key: 'settings', label: 'Cài đặt', icon: '⚙️', collapsedByDefault: true,
    items: [
      { path: '/ai-settings', label: 'Cài đặt AI', icon: '🤖' },
      { path: '/api-keys', label: 'Khóa API', icon: '🔑' },
      { path: '/help', label: 'Hướng dẫn', icon: '❓' },
    ]
  },
];

/**
 * Route cố ý KHÔNG nằm trong menu trái: '' (chuyển hướng), 'auth' (đăng nhập), '**' (không tìm thấy), và hai trang dành riêng cho quản trị
 * (`admin`, `changelog`) vẫn là nút ADMIN/DEV ở thanh trên.
 */
export const NAV_EXCLUDED_ROUTES: readonly string[] = ['', 'auth', 'admin', 'changelog', '**'];

/** Trang nhiều biểu đồ: menu mặc định thu gọn thành thanh biểu tượng để biểu đồ rộng nhất có thể (lựa chọn đã lưu của người dùng luôn thắng). */
export const CHART_ROUTE_PREFIXES: readonly string[] = ['/symbol-timeline', '/trade-plan', '/market-data', '/trade-replay'];

export function allNavItems(): NavItem[] {
  return NAV_GROUPS.flatMap(g => g.items);
}

/** URL (có thể kèm query/hash) có thuộc một trang nhiều biểu đồ không. Khớp theo đoạn đầu trọn vẹn: /trade-planner không khớp /trade-plan. */
export function isChartRoute(url: string): boolean {
  const path = (url ?? '').split(/[?#]/)[0];
  return CHART_ROUTE_PREFIXES.some(p => path === p || path.startsWith(p + '/'));
}
