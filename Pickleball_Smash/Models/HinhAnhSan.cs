namespace Pickleball_Smash.Models
{
    public class HinhAnhSan
    {
        public int HinhAnhID { get; set; }
        public int? SanID { get; set; }
        public string? DuongDanURL { get; set; }
        public SanPickleball? SanPickleball { get; set; }
    }
}