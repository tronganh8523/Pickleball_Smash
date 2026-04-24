namespace Pickleball_Smash.Models.ViewModels
{
    public class ManagerCreateBookingRequest
    {
        public int SanID { get; set; }
        public string? TenKhachHang { get; set; }
        public string? SoDienThoai { get; set; }
        public string? NgayChoi { get; set; }
        public string? GioBatDau { get; set; }
        public string? GioKetThuc { get; set; }
        public List<int>? SelectedHours { get; set; }
    }

    public class ManagerUpdateBookingRequest
    {
        public int DonDatSanID { get; set; }
        public int SanID { get; set; }
        public string? NgayChoi { get; set; }
        public List<int>? SelectedHours { get; set; }
    }
}
