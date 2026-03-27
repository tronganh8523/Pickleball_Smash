using System.Collections.Generic;

namespace Pickleball_Smash.Models.ViewModels
{
    public class UserSanChiTietViewModel
    {
        public SanPickleball San { get; set; } = null!;
        public List<DanhGia> DanhGias { get; set; } = new();
        public double DiemTrungBinh { get; set; }
        public int SoLuongDanhGia { get; set; }
    }
}
