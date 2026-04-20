namespace Pickleball_Smash.Models
{
    public class DonDatSan
    {
        public int DonDatSanID { get; set; }
        public int? NguoiDungID { get; set; }
        public int? SanID { get; set; }
        public int? VoucherID { get; set; }
        public DateOnly? NgayChoi { get; set; }
        public string? KhungGio { get; set; }
        public decimal? TongTien { get; set; }
        public decimal? SoTienGiam { get; set; }
        public string? TrangThaiDon { get; set; }
        public DateTime? NgayTao { get; set; }
        public NguoiDung? NguoiDung { get; set; }
        public SanPickleball? SanPickleball { get; set; }
        public Voucher? Voucher { get; set; }
        public ICollection<ThanhToan>? ThanhToans { get; set; }
    }
}

