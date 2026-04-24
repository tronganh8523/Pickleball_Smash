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
            if (!HasManagerAccess()) return Forbid();

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
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.TenSan })
                .ToList();

            var khungGioOptions = tatCaDon
                .Select(FormatBookingTimeRange)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "--:--")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(label => new SelectListItem { Value = label, Text = label })
                .ToList();

            var trangThaiOptions = tatCaDon
                .Select(d => NormalizeStatus(d.TrangThaiDon))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new SelectListItem { Value = x, Text = x })
                .ToList();

            var filteredDon = tatCaDon.AsEnumerable();

            if (sanId.HasValue && sanId.Value > 0)
                filteredDon = filteredDon.Where(d => d.SanID == sanId.Value);

            if (!string.IsNullOrWhiteSpace(khungGio))
                filteredDon = filteredDon.Where(d => string.Equals(FormatBookingTimeRange(d), khungGio.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(ngayTao) && DateOnly.TryParse(ngayTao, out var selectedNgayTao))
                filteredDon = filteredDon.Where(d => d.NgayTao.HasValue && DateOnly.FromDateTime(d.NgayTao.Value) == selectedNgayTao);

            if (!string.IsNullOrWhiteSpace(trangThai))
                filteredDon = filteredDon.Where(d => string.Equals(NormalizeStatus(d.TrangThaiDon), trangThai.Trim(), StringComparison.OrdinalIgnoreCase));

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
            if (!HasManagerAccess()) return Forbid();
            if (request == null || request.SanID <= 0 || request.BookingID <= 0) return BadRequest(new { success = false, message = "Dữ liệu xác nhận không hợp lệ." });

            var donDat = await _context.DonDatSan.Include(x => x.SanPickleball).FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);
            if (donDat == null) return NotFound(new { success = false, message = "Không tìm thấy đơn đặt sân." });
            if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Chờ xác nhận", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { success = false, message = "Chỉ có thể xác nhận đơn đang chờ xác nhận." });

            donDat.TrangThaiDon = "Đã xác nhận";
            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xác nhận đơn đặt sân." });
        }

        [HttpPost]
        public async Task<IActionResult> CheckoutCourt([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess()) return Forbid();
            if (request == null || request.SanID <= 0 || request.BookingID <= 0) return BadRequest(new { success = false, message = "Dữ liệu check-out không hợp lệ." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var donDat = await _context.DonDatSan
                    .Include(x => x.SanPickleball)
                    .FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);

                if (donDat == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn đang hoạt động của sân." });
                }

                // Chỉ có thể check-out từ các trạng thái đã xác nhận
                var checkoutStatuses = new[] { "Đã xác nhận", "Đang chơi" };
                if (string.IsNullOrWhiteSpace(donDat.TrangThaiDon) || !checkoutStatuses.Contains(donDat.TrangThaiDon, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "Vui lòng xác nhận đơn trước khi check-out." });
                }

                donDat.TrangThaiDon = "Hoàn thành";
                var san = donDat.SanPickleball;
                if (san != null)
                {
                    san.TrangThai = "Trống";
                    _context.SanPickleball.Update(san);
                }

                var hasPayment = await _context.ThanhToan.AnyAsync(x => x.DonDatSanID == donDat.DonDatSanID);
                if (!hasPayment)
                {
                    var payment = new ThanhToan
                    {
                        DonDatSanID = donDat.DonDatSanID,
                        PhuongThuc = "Tiền mặt tại quầy",
                        SoTien = donDat.TongTien ?? 0,
                        MaGiaoDich = $"MGR-{donDat.DonDatSanID}-{DateTime.Now:yyyyMMddHHmmss}",
                        TrangThai = "Hoàn thành",
                        NgayThanhToan = DateTime.Now
                    };

                    _context.ThanhToan.Add(payment);
                }

                _context.DonDatSan.Update(donDat);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Đã check-out và giải phóng sân." });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Không thể check-out. Vui lòng thử lại." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelBooking([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess()) return Forbid();
            if (request == null || request.SanID <= 0 || request.BookingID <= 0) return BadRequest(new { success = false, message = "Dữ liệu hủy đơn không hợp lệ." });

            var donDat = await _context.DonDatSan.FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);
            if (donDat == null) return NotFound(new { success = false, message = "Không tìm thấy đơn đặt sân." });
            if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Chờ xác nhận", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { success = false, message = "Chỉ có thể hủy đơn đang chờ xác nhận." });

            donDat.TrangThaiDon = "Đã hủy";
            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã hủy đơn đặt sân." });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingForEdit(int id)
        {
            if (!HasManagerAccess()) return Json(new { success = false, message = "Không có quyền truy cập." });

            var donDat = await _context.DonDatSan
                .Include(d => d.NguoiDung)
                .FirstOrDefaultAsync(d => d.DonDatSanID == id);

            if (donDat == null) return Json(new { success = false, message = "Không tìm thấy đơn đặt sân." });
            if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Chờ xác nhận", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Chỉ có thể sửa đơn đang chờ xác nhận." });

            var khungGioHours = new List<int>();
            if (!string.IsNullOrEmpty(donDat.KhungGio))
            {
                var parts = donDat.KhungGio.Split(',', StringSplitOptions.RemoveEmptyEntries);
                khungGioHours = parts.Select(p => int.TryParse(p.Trim(), out var h) ? h : -1)
                    .Where(h => h >= 0)
                    .OrderBy(h => h)
                    .ToList();
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    donDatSanID = donDat.DonDatSanID,
                    sanID = donDat.SanID,
                    tenKhachHang = donDat.NguoiDung?.HoTen ?? "-",
                    soDienThoai = donDat.NguoiDung?.SDT ?? "-",
                    ngayChoi = donDat.NgayChoi?.ToString("yyyy-MM-dd") ?? "",
                    khungGio = donDat.KhungGio ?? "",
                    selectedHours = khungGioHours,
                    tongTien = donDat.TongTien ?? 0
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBooking([FromBody] ManagerUpdateBookingRequest request)
        {
            if (!HasManagerAccess()) return Json(new { success = false, message = "Không có quyền truy cập." });
            if (request == null || request.DonDatSanID <= 0) return Json(new { success = false, message = "Dữ liệu cập nhật không hợp lệ." });

            var donDat = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .FirstOrDefaultAsync(d => d.DonDatSanID == request.DonDatSanID);

            if (donDat == null) return Json(new { success = false, message = "Không tìm thấy đơn đặt sân." });
            if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Chờ xác nhận", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Chỉ có thể sửa đơn đang chờ xác nhận." });

            if (!DateOnly.TryParse(request.NgayChoi, out var ngayChoi))
                return Json(new { success = false, message = "Ngày chơi không hợp lệ." });

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (ngayChoi < today)
                return Json(new { success = false, message = "Không thể đặt sân cho ngày trong quá khứ." });

            List<int> selectedHours;
            if (request.SelectedHours != null && request.SelectedHours.Any())
            {
                selectedHours = request.SelectedHours
                    .Where(h => h >= 5 && h <= 23)
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();
            }
            else
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một khung giờ." });
            }

            // Kiểm tra không được chọn khung giờ quá khứ (cho ngày hôm nay)
            var now = DateTime.Now;
            if (ngayChoi == today)
            {
                var passedHours = selectedHours.Where(h => h <= now.Hour).ToList();
                if (passedHours.Any())
                {
                    var passedTimes = string.Join(", ", passedHours.Select(h => $"{h:D2}:00"));
                    return Json(new { success = false, message = $"Khung giờ {passedTimes} đã quá. Vui lòng chọn khung giờ khác." });
                }
            }

            // Check for time slot conflicts
            var conflictBookings = await _context.DonDatSan
                .Where(d => d.SanID == request.SanID
                    && d.NgayChoi == ngayChoi
                    && d.DonDatSanID != request.DonDatSanID
                    && (d.TrangThaiDon == "Chờ xác nhận" 
                        || d.TrangThaiDon == "Đã xác nhận" 
                        || d.TrangThaiDon == "Đang chơi"
                        || d.TrangThaiDon == "Đã đặt"))
                .ToListAsync();

            if (conflictBookings.Any())
            {
                var occupiedHours = new List<int>();
                foreach (var booking in conflictBookings)
                {
                    if (!string.IsNullOrEmpty(booking.KhungGio))
                    {
                        var hours = booking.KhungGio.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(h => int.TryParse(h.Trim(), out var hour) ? hour : -1)
                            .Where(h => h >= 0)
                            .ToList();
                        occupiedHours.AddRange(hours);
                    }
                }

                var conflicts = selectedHours.Intersect(occupiedHours).ToList();
                if (conflicts.Any())
                {
                    var conflictTimes = string.Join(", ", conflicts.Select(h => $"{h:D2}:00"));
                    return Json(new { success = false, message = $"Khung giờ {conflictTimes} đã có người đặt. Vui lòng chọn khung giờ khác." });
                }
            }

            // Get available price for the selected time slots
            var bangGia = await _context.BangGiaKhungGio
                .Where(b => b.SanID == request.SanID)
                .FirstOrDefaultAsync();

            if (bangGia == null)
                return Json(new { success = false, message = "Không tìm thấy giá cho sân này." });

            var khungGioStr = string.Join(",", selectedHours);
            var tongTien = selectedHours.Count * (bangGia.GiaTien ?? 0);

            // Update booking
            donDat.SanID = request.SanID;
            donDat.NgayChoi = ngayChoi;
            donDat.KhungGio = khungGioStr;
            donDat.TongTien = tongTien;

            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã cập nhật đơn đặt sân.", data = new { tongTien = tongTien } });
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTimeSlots(int sanId, string ngayChoi)
        {
            if (!HasManagerAccess()) return Json(new { success = false, message = "Không có quyền truy cập." });
            
            if (!DateOnly.TryParse(ngayChoi, out var selectedDate))
                return Json(new { success = false, message = "Ngày không hợp lệ." });

            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.Now;
            
            // Lấy tất cả các đơn đặt sân không bị hủy cho sân này vào ngày đó
            var bookedSlots = await _context.DonDatSan
                .Where(d => d.SanID == sanId
                    && d.NgayChoi == selectedDate
                    && (d.TrangThaiDon == "Chờ xác nhận" 
                        || d.TrangThaiDon == "Đã xác nhận" 
                        || d.TrangThaiDon == "Đang chơi"
                        || d.TrangThaiDon == "Đã đặt"))
                .Select(d => d.KhungGio)
                .ToListAsync();

            var occupiedHours = new List<int>();
            foreach (var khungGio in bookedSlots)
            {
                if (!string.IsNullOrEmpty(khungGio))
                {
                    var hours = khungGio.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(h => int.TryParse(h.Trim(), out var hour) ? hour : -1)
                        .Where(h => h >= 0)
                        .ToList();
                    occupiedHours.AddRange(hours);
                }
            }

            // Tạo danh sách tất cả các slot và đánh dấu occupied
            var allSlots = new List<object>();
            for (int hour = 5; hour <= 23; hour++)
            {
                bool isOccupied = occupiedHours.Contains(hour);
                // Slot quá khứ: nếu hôm nay và giờ <= giờ hiện tại thì không thể chọn
                bool isPassed = selectedDate == today && hour <= now.Hour;
                
                allSlots.Add(new
                {
                    hour = hour,
                    isOccupied = isOccupied,
                    isPassed = isPassed,
                    disabled = isOccupied || isPassed,
                    label = isOccupied ? "Đã đặt" : (isPassed ? "Quá giờ" : "Có sẵn")
                });
            }

            return Json(new { success = true, data = allSlots });
        }

        private bool HasManagerAccess()
        {
            var role = HttpContext.Session.GetString("VaiTro");
            if (string.IsNullOrWhiteSpace(role)) return true;
            return role.Equals("Manager", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatBookingTimeRange(DonDatSan booking)
        {
            if (string.IsNullOrEmpty(booking.KhungGio)) return "--:--";

            var parts = booking.KhungGio.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "--:--";

            var hours = parts.Select(p => int.TryParse(p.Trim(), out var h) ? h : -1).Where(h => h != -1).OrderBy(h => h).ToList();
            if (!hours.Any()) return "--:--";

            var result = new List<string>();
            int start = hours[0];
            int end = hours[0] + 1;

            for (int i = 1; i < hours.Count; i++)
            {
                if (hours[i] == end) end = hours[i] + 1;
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

        private static string NormalizeStatus(string? status)
        {
            var value = status?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}