namespace Pickleball_Smash.Models.ViewModels
{
    public class BangGiaKhungGioCreateViewModel
    {
        public List<int> SanIDs { get; set; } = new();
        public string? KhungGio { get; set; }
        public decimal? GiaTien { get; set; }
    }
}