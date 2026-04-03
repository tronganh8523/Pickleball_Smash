using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Pickleball_Smash.Models.ViewModels;

namespace Pickleball_Smash.Controllers
{
    public class ManagerDonDatSanController : Controller
    {
        private readonly AppDbContext _context;

        public ManagerDonDatSanController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Bookings(int? sanId, string? khungGio, string? ngayTao, string? trangThai)
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            var tatCaDon = await _context.DonDatSan
                .AsNoTracking()
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .Where(d => d.TrangThaiDon == null || (d.TrangThaiDon != "Hoàn thành" && d.TrangThaiDon != "Đã hủy"))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            var sanOptions = tatCaDon
                .Where(d => d.SanPickleball != null)
                .Select(d => new { Id = d.SanID ?? 0, TenSan = d.SanPickleball!.TenSan ?? "N/A" })
                .Where(x => x.Id > 0)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.TenSan)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.TenSan
                })
                .ToList();

            var khungGioOptions = tatCaDon
                .Select(FormatBookingTimeRange)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(label => new SelectListItem
                {
                    Value = label,
                    Text = label
                })
                .ToList();

            var trangThaiOptions = tatCaDon
                .Select(d => NormalizeStatus(d.TrangThaiDon))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new SelectListItem
                {
                    Value = x,
                    Text = x
                })
                .ToList();

            var filteredDon = tatCaDon.AsEnumerable();

            if (sanId.HasValue && sanId.Value > 0)
            {
                filteredDon = filteredDon.Where(d => d.SanID == sanId.Value);
            }

            if (!string.IsNullOrWhiteSpace(khungGio))
            {
                filteredDon = filteredDon.Where(d => string.Equals(FormatBookingTimeRange(d), khungGio.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(ngayTao) && DateOnly.TryParse(ngayTao, out var selectedNgayTao))
            {
                filteredDon = filteredDon.Where(d => d.NgayTao.HasValue && DateOnly.FromDateTime(d.NgayTao.Value) == selectedNgayTao);
            }

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                filteredDon = filteredDon.Where(d => string.Equals(NormalizeStatus(d.TrangThaiDon), trangThai.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            ViewBag.SanOptions = (object)sanOptions;
            ViewBag.KhungGioOptions = (object)khungGioOptions;
            ViewBag.TrangThaiOptions = (object)trangThaiOptions;
            ViewBag.SelectedSanId = sanId;
            ViewBag.SelectedKhungGio = khungGio;
            ViewBag.SelectedNgayTao = ngayTao;
            ViewBag.SelectedTrangThai = trangThai;

            return View("~/Views/Manager/Bookings.cshtml", filteredDon.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            if (request == null || request.SanID <= 0 || request.BookingID <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu xác nhận không hợp lệ." });
            }

            var donDat = await _context.DonDatSan
                .Include(x => x.SanPickleball)
                .FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);

            if (donDat == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đơn đặt sân." });
            }

            if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Chờ xác nhận", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Chỉ có thể xác nhận đơn đang chờ xác nhận." });
            }

            donDat.TrangThaiDon = "Đã xác nhận";
            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xác nhận đơn đặt sân." });
        }

        [HttpPost]
        public async Task<IActionResult> CheckoutCourt([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            if (request == null || request.SanID <= 0 || request.BookingID <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu check-out không hợp lệ." });
            }

            var donDat = await _context.DonDatSan
                .Include(x => x.SanPickleball)
                .FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);

            if (donDat == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đơn đang hoạt động của sân." });
            }

            var activeStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chơi", "Đã đặt" };
            if (string.IsNullOrWhiteSpace(donDat.TrangThaiDon)
                || !activeStatuses.Contains(donDat.TrangThaiDon, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Đơn này không ở trạng thái có thể check-out." });
            }

            donDat.TrangThaiDon = "Hoàn thành";

            var san = donDat.SanPickleball;
            if (san != null)
            {
                san.TrangThai = "Trống";
                _context.SanPickleball.Update(san);
            }

            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã check-out và giải phóng sân." });
        }

        [HttpPost]
        public async Task<IActionResult> CancelBooking([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            if (request == null || request.SanID <= 0 || request.BookingID <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu hủy đơn không hợp lệ." });
            }

            var donDat = await _context.DonDatSan
                .FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);

            if (donDat == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đơn đặt sân." });
            }

            if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Chờ xác nhận", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Chỉ có thể hủy đơn đang chờ xác nhận." });
            }

            donDat.TrangThaiDon = "Đã hủy";
            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã hủy đơn đặt sân." });
        }

        private bool HasManagerAccess()
        {
            var role = HttpContext.Session.GetString("VaiTro");
            if (string.IsNullOrWhiteSpace(role))
            {
                return true;
            }

            return role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatBookingTimeRange(DonDatSan booking)
        {
            var start = booking.ThoiGianBatDau.HasValue
                ? booking.ThoiGianBatDau.Value.ToString("HH\\:mm")
                : "--:--";

            var end = booking.ThoiGianKetThuc.HasValue
                ? booking.ThoiGianKetThuc.Value == TimeOnly.MinValue
                    ? "24:00"
                    : booking.ThoiGianKetThuc.Value.ToString("HH\\:mm")
                : "--:--";

            return $"{start} - {end}";
        }

        private static string NormalizeStatus(string? status)
        {
            var value = status?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}