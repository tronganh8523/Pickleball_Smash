using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class SanController : Controller
    {
        private readonly AppDbContext _context;

        public SanController(AppDbContext context)
        {
            _context = context;
        }

        // Trang Danh sách sân cho Khách hàng
        public async Task<IActionResult> Index(string? searchQuery, string? loaiSan, string? mucGia)
        {
            var query = _context.SanPickleball
                .Include(s => s.HinhAnhSans)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(s => s.TenSan.Contains(searchQuery.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(loaiSan))
            {
                query = query.Where(s => s.LoaiSan == loaiSan);
            }

            if (!string.IsNullOrWhiteSpace(mucGia))
            {
                if (mucGia == "duoi100") query = query.Where(s => s.GiaCoBan < 100000);
                else if (mucGia == "tren100") query = query.Where(s => s.GiaCoBan >= 100000);
            }

            var courts = await query.ToListAsync();
            return View(courts);
        }

        [HttpGet]
        public async Task<IActionResult> GetBookedSlots(int sanId, DateTime date)
        {
            var bookedHours = new List<int>();

            // Tìm các đơn không bị hủy của sân này trong ngày được chọn
            var bookings = await _context.DonDatSan
                .Where(b => b.SanID == sanId
                         && b.NgayChoi == DateOnly.FromDateTime(date)
                         && b.TrangThaiDon != "Đã hủy")
                .ToListAsync();

            foreach (var b in bookings)
            {
                if (b.ThoiGianBatDau.HasValue && b.ThoiGianKetThuc.HasValue)
                {
                    int start = b.ThoiGianBatDau.Value.Hour;
                    int end = b.ThoiGianKetThuc.Value.Hour;
                    // Đưa các giờ nằm trong khoảng thời gian đã đặt vào danh sách khóa
                    for (int i = start; i < end; i++)
                    {
                        bookedHours.Add(i);
                    }
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

            List<int> bookingIds = new List<int>();
            decimal pricePerSlot = request.TongTien / request.SelectedHours.Count;

            // Tạo 1 record DonDatSan cho MỖI khung giờ 1 tiếng
            foreach (var hour in request.SelectedHours)
            {
                var donDat = new DonDatSan
                {
                    NguoiDungID = userId.Value,
                    SanID = request.SanID,
                    NgayChoi = DateOnly.FromDateTime(request.NgayDat),
                    ThoiGianBatDau = new TimeOnly(hour, 0),
                    ThoiGianKetThuc = new TimeOnly(hour + 1, 0),
                    TongTien = pricePerSlot,
                    TrangThaiDon = "Chờ thanh toán",
                    NgayTao = DateTime.Now
                };
                _context.DonDatSan.Add(donDat);
                await _context.SaveChangesAsync();
                bookingIds.Add(donDat.DonDatSanID);
            }

            // Trả về danh sách ID các đơn vừa tạo
            return Json(new { success = true, bookingIds = bookingIds });
        }


        // API 2: Xác nhận thanh toán
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

        // API 3: Lấy lịch sử giao dịch của user đang đăng nhập
        [HttpGet]
        public async Task<IActionResult> GetBookingHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized();

            var history = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .Where(d => d.NguoiDungID == userId && d.TrangThaiDon == "Đã thanh toán")
                .OrderByDescending(d => d.NgayTao)
                .Select(d => new {
                    maHoaDon = d.DonDatSanID.ToString("D3"),
                    ngayThanhToan = d.NgayTao.HasValue ? d.NgayTao.Value.ToString("dd/MM/yyyy") : "",
                    loaiSan = d.SanPickleball.LoaiSan,
                    khungGio = $"{d.ThoiGianBatDau:hh\\:mm} - {d.ThoiGianKetThuc:hh\\:mm}",
                    tongTien = d.TongTien,
                    trangThai = d.TrangThaiDon
                })
                .ToListAsync();

            return Json(history);
        }
    }

    // Class nhận dữ liệu từ JS
    public class BookingRequest
    {
        public int SanID { get; set; }
        public DateTime NgayDat { get; set; }
        public List<int> SelectedHours { get; set; } // Danh sách các giờ được chọn (ví dụ: [5, 6, 18])
        public string GhiChu { get; set; }
        public decimal TongTien { get; set; }
    }
}
