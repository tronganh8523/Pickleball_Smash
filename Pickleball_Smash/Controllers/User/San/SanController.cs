using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers.User.San
{
    public class SanController : Controller
    {
        private readonly AppDbContext _context;

        public SanController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchQuery, string? loaiSan, string? mucGia)
        {
            var query = _context.SanPickleball
                .Include(s => s.HinhAnhSans)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
                query = query.Where(s => s.TenSan.Contains(searchQuery.Trim()));

            if (!string.IsNullOrWhiteSpace(loaiSan))
                query = query.Where(s => s.LoaiSan == loaiSan);

            if (!string.IsNullOrWhiteSpace(mucGia))
            {
                if (mucGia == "duoi100") query = query.Where(s => s.GiaCoBan < 100000);
                else if (mucGia == "tren100") query = query.Where(s => s.GiaCoBan >= 100000);
            }

            var courts = await query.ToListAsync();
            var today = DateOnly.FromDateTime(DateTime.Now);

            foreach (var san in courts)
            {
                var bookings = await _context.DonDatSan
                    .Where(d => d.SanID == san.SanID
                             && d.NgayChoi == today
                             && d.TrangThaiDon != "Đã hủy")
                    .ToListAsync();

                int soGioDaDat = 0;
                foreach (var b in bookings)
                {
                    if (!string.IsNullOrEmpty(b.KhungGio))
                        soGioDaDat += b.KhungGio.Split(',').Length;
                }

                if (soGioDaDat >= 17) san.TrangThai = "Bận";
                else san.TrangThai = "Trống";
            }

            return View(courts);
        }

        [HttpGet]
        public async Task<IActionResult> GetBookedSlots(int sanId, DateTime date)
        {
            var bookedHours = new List<int>();

            // LỖI 1 ĐÃ FIX: Phải tách DateOnly ra một biến riêng TRƯỚC KHI đưa vào LINQ
            DateOnly targetDate = DateOnly.FromDateTime(date);

            var bookings = await _context.DonDatSan
                .Where(b => b.SanID == sanId
                         && b.NgayChoi == targetDate
                         && b.TrangThaiDon != "Đã hủy")
                .ToListAsync();

            foreach (var b in bookings)
            {
                if (!string.IsNullOrEmpty(b.KhungGio))
                {
                    // LỖI 2 ĐÃ FIX: Dùng TryParse và Trim() để chống sập khi chuỗi có khoảng trắng
                    var hours = b.KhungGio.Split(',')
                                          .Select(h => int.TryParse(h.Trim(), out var val) ? val : -1)
                                          .Where(h => h != -1)
                                          .ToList();
                    bookedHours.AddRange(hours);
                }
            }
            return Json(bookedHours.Distinct());
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] BookingRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            if (request.SelectedHours == null || !request.SelectedHours.Any())
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 khung giờ" });

            string chuoiKhungGio = string.Join(",", request.SelectedHours.OrderBy(h => h));

            var donDat = new DonDatSan
            {
                NguoiDungID = userId.Value,
                SanID = request.SanID,
                NgayChoi = DateOnly.FromDateTime(request.NgayDat),
                KhungGio = chuoiKhungGio,
                TongTien = request.TongTien,
                TrangThaiDon = "Chờ thanh toán",
                NgayTao = DateTime.Now
            };

            _context.DonDatSan.Add(donDat);
            await _context.SaveChangesAsync();

            return Json(new { success = true, bookingIds = new[] { donDat.DonDatSanID } });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] List<int> bookingIds)
        {
            var donDats = await _context.DonDatSan
                                        .Where(d => bookingIds.Contains(d.DonDatSanID))
                                        .ToListAsync();
            foreach (var donDat in donDats)
            {
                donDat.TrangThaiDon = "Đã thanh toán";
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized();

            var history = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .Where(d => d.NguoiDungID == userId && d.TrangThaiDon == "Đã thanh toán")
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            var result = history.Select(d => new {
                maHoaDon = d.DonDatSanID.ToString("D3"),
                ngayThanhToan = d.NgayTao.HasValue ? d.NgayTao.Value.ToString("dd/MM/yyyy") : "",
                loaiSan = d.SanPickleball != null ? d.SanPickleball.LoaiSan : string.Empty,
                khungGio = FormatKhungGioHienThi(d.KhungGio),
                tongTien = d.TongTien,
                trangThai = d.TrangThaiDon
            }).ToList();

            return Json(result);
        }

        // =========================================================================
        // THUẬT TOÁN GỘP CHUỖI CẢI TIẾN CHỐNG CRASH
        // =========================================================================
        private string FormatKhungGioHienThi(string? khungGioStr)
        {
            if (string.IsNullOrEmpty(khungGioStr)) return "";

            var parts = khungGioStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Dùng int.TryParse để nếu chuỗi có bị lỗi định dạng thì web vẫn không sập
            var hours = parts.Select(p => int.TryParse(p.Trim(), out var h) ? h : -1)
                             .Where(h => h != -1)
                             .OrderBy(h => h)
                             .ToList();

            if (!hours.Any()) return "";

            var result = new List<string>();
            int start = hours[0];
            int end = hours[0] + 1;

            for (int i = 1; i < hours.Count; i++)
            {
                if (hours[i] == end)
                {
                    end = hours[i] + 1;
                }
                else
                {
                    result.Add($"{start:D2}:00 - {end:D2}:00");
                    start = hours[i];
                    end = hours[i] + 1;
                }
            }
            result.Add($"{start:D2}:00 - {end:D2}:00");

            return string.Join(", ", result);
        }
    }

    public class BookingRequest
    {
        public int SanID { get; set; }
        public DateTime NgayDat { get; set; }
        public List<int> SelectedHours { get; set; } = new();
        public string? GhiChu { get; set; }
        public decimal TongTien { get; set; }
    }
}