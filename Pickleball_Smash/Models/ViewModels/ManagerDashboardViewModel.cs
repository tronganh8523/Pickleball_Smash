using Pickleball_Smash.Models;

namespace Pickleball_Smash.Models.ViewModels
{
    public class ManagerDashboardViewModel
    {
        public int TongSan { get; set; }
        public int TongSanConTrong { get; set; }
        public int TongDonHomNay { get; set; }
        public int TongDonChoXacNhan { get; set; }
        public int TongSanDangBan { get; set; }

        public List<ManagerCourtCardViewModel> DanhSachSan { get; set; } = new();
        public List<DonDatSan> DonGanDay { get; set; } = new();
    }

    public class ManagerCourtCardViewModel
    {
        public int SanID { get; set; }
        public int? BookingDangHoatDongID { get; set; }
        public bool CanBook { get; set; } = true;
        public string TenSan { get; set; } = string.Empty;
        public string LoaiSan { get; set; } = "Chưa cập nhật";
        public decimal GiaCoBan { get; set; }
        public string TinhTrang { get; set; } = "Trống";
        public string BadgeClass { get; set; } = "status-open";
        public string ActionClass { get; set; } = "btn-book";
        public string ActionText { get; set; } = "Đặt sân ngay";
        public string MoTaNgan { get; set; } = "Sân đạt tiêu chuẩn thi đấu, phù hợp cho mọi trình độ.";
        public string AnhDaiDienUrl { get; set; } = "https://placehold.co/640x360?text=Pickleball+Court";
    }
}
