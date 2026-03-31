using Microsoft.AspNetCore.Mvc;

namespace Pickleball_Smash.Models.ViewModels
{
    public class StaffDashboardViewModel
    {
        public string HoTenNhanVien { get; set; } = "Nhân viên";
        public string? SearchQuery { get; set; }
        public string? SelectedLoaiSan { get; set; }
        public string? SelectedTrangThai { get; set; }
        public List<StaffCourtCardViewModel> DanhSachSan { get; set; } = new();
    }

    public class StaffCourtCardViewModel
    {
        public int SanID { get; set; }
        public string TenSan { get; set; } = string.Empty;
        public string LoaiSan { get; set; } = string.Empty;
        public string TrangThai { get; set; } = string.Empty;
        public string AnhDaiDienUrl { get; set; } = "/Img/SanMau1.jpg";
    }
}