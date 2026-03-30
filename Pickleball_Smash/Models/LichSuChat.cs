namespace Pickleball_Smash.Models
{
    public class LichSuChat
    {
        public int ChatID { get; set; }
        public int? NguoiDungID { get; set; }
        public string? NoiDungHoi { get; set; }
        public string? PhanHoiAI { get; set; }
        public DateTime? ThoiGian { get; set; }
        public NguoiDung? NguoiDung { get; set; }
    }
}

