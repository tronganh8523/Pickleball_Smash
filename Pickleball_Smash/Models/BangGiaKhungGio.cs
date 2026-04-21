namespace Pickleball_Smash.Models
{
    public class BangGiaKhungGio
    {
        public int MaGia { get; set; }
        public int? SanID { get; set; }
        public string? KhungGio { get; set; }
        public decimal? GiaTien { get; set; }
        public SanPickleball? SanPickleball { get; set; }
    }
}