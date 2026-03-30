namespace Pickleball_Smash.Models
{
    public class SanPickleball
    {
        public int SanID { get; set; }
        public string TenSan { get; set; } = null!;
        public string? LoaiSan { get; set; }
        public string? MoTa { get; set; }
        public decimal? GiaCoBan { get; set; }
        public string? TrangThai { get; set; }

        // Navigation properties
        public ICollection<DonDatSan>? DonDatSans { get; set; }
        public ICollection<DanhGia>? DanhGias { get; set; }
        public ICollection<HinhAnhSan>? HinhAnhSans { get; set; }
        public ICollection<BangGiaKhungGio>? BangGiaKhungGios { get; set; }
    }
}

