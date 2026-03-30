namespace Pickleball_Smash.Models.ViewModels
{
    public class BangGiaKhungGioCreateViewModel
    {
        public List<int> SanIDs { get; set; } = new();
        public TimeOnly? GioBatDau { get; set; }
        public TimeOnly? GioKetThuc { get; set; }
        public decimal? GiaTien { get; set; }
    }
}