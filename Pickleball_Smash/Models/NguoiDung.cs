namespace Pickleball_Smash.Models
{
    public class NguoiDung
    {
        public int NguoiDungID { get; set; }
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;
        public string? Email { get; set; }
        public string? HoTen { get; set; }
        public string? GioiTinh { get; set; }
        public string? SDT { get; set; }
        public string? VaiTro { get; set; }
        public DateTime? NgayTao { get; set; }

        // Navigation properties
        public ICollection<DonDatSan>? DonDatSans { get; set; }
        public ICollection<DanhGia>? DanhGias { get; set; }
        public ICollection<LichSuChat>? LichSuChats { get; set; }
    }
}

