namespace Pickleball_Smash.Models
{
    public class DanhGia
    {
        public int DanhGiaID { get; set; }
        public int? NguoiDungID { get; set; }
        public int? SanID { get; set; }
        public int? SoSao { get; set; }
        public string? BinhLuan { get; set; }
        public DateTime? NgayDanhGia { get; set; }
        public NguoiDung? NguoiDung { get; set; }
        public SanPickleball? SanPickleball { get; set; }
    }
}

