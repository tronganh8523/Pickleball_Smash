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

        public async Task<IActionResult> Bookings(int? sanId, string? khungGio, string? ngayChoiTu, string? ngayChoiDen, string? trangThai, string? sapXep, string? tuKhoa)
        {
            if (!HasManagerAccess()) return NotFound();
            await AutoCheckoutExpiredConfirmedBookings();

            var tatCaDon = await _context.DonDatSan
                .AsNoTracking()
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .Include(d => d.ThanhToans)
                .Where(d => d.TrangThaiDon == null || (d.TrangThaiDon != "Hoàn thành" && d.TrangThaiDon != "Đã hủy"))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            // Lấy từ danh mục sân để luôn hiển thị đủ sân (kể cả sân chưa có đơn)
            var sanOptions = await _context.SanPickleball
                .AsNoTracking()
                .Where(s => s.TrangThai != null && s.TrangThai.Trim() == "Trống")
                .OrderBy(s => s.TenSan)
                .Select(s => new SelectListItem { Value = s.SanID.ToString(), Text = s.TenSan ?? $"Sân {s.SanID}" })
                .ToListAsync();

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

            if (!string.IsNullOrWhiteSpace(ngayChoiTu) && DateOnly.TryParse(ngayChoiTu, out var fromDate))
                filteredDon = filteredDon.Where(d => d.NgayChoi.HasValue && d.NgayChoi.Value >= fromDate);

            if (!string.IsNullOrWhiteSpace(ngayChoiDen) && DateOnly.TryParse(ngayChoiDen, out var toDate))
                filteredDon = filteredDon.Where(d => d.NgayChoi.HasValue && d.NgayChoi.Value <= toDate);

            if (!string.IsNullOrWhiteSpace(trangThai))
                filteredDon = filteredDon.Where(d => string.Equals(NormalizeStatus(d.TrangThaiDon), trangThai.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var keyword = tuKhoa.Trim();
                filteredDon = filteredDon.Where(d =>
                    (!string.IsNullOrWhiteSpace(d.NguoiDung?.HoTen) && d.NguoiDung.HoTen.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(d.NguoiDung?.TenDangNhap) && d.NguoiDung.TenDangNhap.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(d.NguoiDung?.SDT) && d.NguoiDung.SDT.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            filteredDon = (sapXep ?? "created_desc").Trim().ToLowerInvariant() switch
            {
                "created_asc" => filteredDon.OrderBy(d => d.NgayTao),
                "playdate_desc" => filteredDon.OrderByDescending(d => d.NgayChoi),
                "playdate_asc" => filteredDon.OrderBy(d => d.NgayChoi),
                "total_desc" => filteredDon.OrderByDescending(d => d.TongTien ?? 0),
                "total_asc" => filteredDon.OrderBy(d => d.TongTien ?? 0),
                _ => filteredDon.OrderByDescending(d => d.NgayTao)
            };

            ViewBag.SanOptions = (object)sanOptions;
            ViewBag.KhungGioOptions = (object)khungGioOptions;
            ViewBag.TrangThaiOptions = (object)trangThaiOptions;
            ViewBag.SelectedSanId = sanId;
            ViewBag.SelectedKhungGio = khungGio;
            ViewBag.SelectedNgayChoiTu = ngayChoiTu;
            ViewBag.SelectedNgayChoiDen = ngayChoiDen;
            ViewBag.SelectedTrangThai = trangThai;
            ViewBag.SelectedSapXep = sapXep;
            ViewBag.SelectedTuKhoa = tuKhoa;

            return View("~/Views/Manager/Bookings.cshtml", filteredDon.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess()) return NotFound();
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
            if (!HasManagerAccess()) return NotFound();
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
            if (!HasManagerAccess()) return NotFound();
            if (request == null || request.SanID <= 0 || request.BookingID <= 0) return BadRequest(new { success = false, message = "Dữ liệu hủy đơn không hợp lệ." });

            var donDat = await _context.DonDatSan.Include(d => d.ThanhToans).FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);
            if (donDat == null) return NotFound(new { success = false, message = "Không tìm thấy đơn đặt sân." });

            // Tính toán thời gian
            var hours = donDat.KhungGio?.Split(',').Select(p => int.TryParse(p.Trim(), out var h) ? h : -1).Where(h => h >= 0).OrderBy(h => h).ToList();
            if (hours == null || !hours.Any() || !donDat.NgayChoi.HasValue) return BadRequest(new { success = false, message = "Lỗi dữ liệu thời gian sân." });

            var earliestHour = hours.First();
            var playDateTime = donDat.NgayChoi.Value.ToDateTime(new TimeOnly(earliestHour, 0));
            var timeDiff = (playDateTime - DateTime.Now).TotalMinutes;

            decimal daThanhToan = donDat.ThanhToans?.Sum(t => t.SoTien) ?? 0;
            string thongBao = "";

            if (daThanhToan > 0)
            {
                if (timeDiff < 60)
                {
                    donDat.TrangThaiDon = "Đã hủy"; // Khách mất cọc/tiền
                    thongBao = "Đã hủy đơn. Gần sát giờ chơi (dưới 60 phút) nên KHÔNG hoàn tiền.";
                }
                else
                {
                    donDat.TrangThaiDon = "Đã hoàn tiền"; // Khách được hoàn
                    thongBao = $"Đã hủy và hoàn lại {daThanhToan:N0}đ cho khách.";
                }
            }
            else
            {
                donDat.TrangThaiDon = "Đã hủy";
                thongBao = "Đã hủy đơn.";
            }

            donDat.YeuCauHuy = false; // Tắt cờ yêu cầu
            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = thongBao });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingForEdit(int id)
        {
            if (!HasManagerAccess()) return NotFound();

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

            var giaTien = await _context.BangGiaKhungGio
                .Where(b => b.SanID == donDat.SanID)
                .Select(b => b.GiaTien ?? 0)
                .FirstOrDefaultAsync();

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
                    tongTien = donDat.TongTien ?? 0,
                    pricePerHour = giaTien
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBooking([FromBody] ManagerUpdateBookingRequest request)
        {
            if (!HasManagerAccess()) return NotFound();
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
                var passedHours = selectedHours
                    .Where(h => DateOnly.FromDateTime(now) == today && new DateTime(now.Year, now.Month, now.Day, h, 0, 0) <= now)
                    .ToList();

                if (passedHours.Any())
                {
                    var passedTimes = string.Join(", ", passedHours.Select(h => $"{h:D2}:00"));
                    return Json(new { success = false, message = $"Khung giờ {passedTimes} đã quá. Vui lòng chọn khung giờ khác." });
                }
            }

            // Check for time slot conflicts
            var blockedStatuses = new[] { "Hoàn thành", "Đã hoàn thành", "Đã hủy", "Đã huỷ", "Thất bại" };
            var conflictBookings = await _context.DonDatSan
                .Where(d => d.SanID == request.SanID
                    && d.NgayChoi == ngayChoi
                    && d.DonDatSanID != request.DonDatSanID
                    && d.TrangThaiDon != null
                    && !blockedStatuses.Contains(d.TrangThaiDon.Trim()))
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

            var khungGioStr = string.Join(",", selectedHours);
            var bangGiaKhungGio = await _context.BangGiaKhungGio
                .AsNoTracking()
                .Where(x => x.SanID == request.SanID && !string.IsNullOrWhiteSpace(x.KhungGio) && x.GiaTien.HasValue)
                .Select(x => new
                {
                    x.KhungGio,
                    GiaTien = x.GiaTien
                })
                .ToListAsync();

            var giaTheoGio = new Dictionary<int, decimal>();
            foreach (var row in bangGiaKhungGio)
            {
                if (!TryParseKhungGioHours(row.KhungGio, out var hours))
                {
                    continue;
                }

                foreach (var hour in hours)
                {
                    if (!giaTheoGio.ContainsKey(hour))
                    {
                        giaTheoGio[hour] = row.GiaTien!.Value;
                    }
                }
            }

            var sanForPrice = await _context.SanPickleball
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SanID == request.SanID);

            var giaCoBan = sanForPrice?.GiaCoBan ?? 0;
            decimal tongTien = 0;
            foreach (var hour in selectedHours)
            {
                var donGia = giaTheoGio.TryGetValue(hour, out var giaTheoKhung) ? giaTheoKhung : giaCoBan;
                if (donGia <= 0)
                {
                    return Json(new { success = false, message = "Sân chưa được cấu hình giá hợp lệ cho khung giờ đã chọn." });
                }

                tongTien += donGia;
            }

            // Update booking
            donDat.SanID = request.SanID;
            donDat.NgayChoi = ngayChoi;
            donDat.KhungGio = khungGioStr;
            donDat.TongTien = tongTien;
            donDat.YeuCauSua = false; 
            donDat.NoiDungSua = null;

            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã cập nhật đơn đặt sân.", data = new { tongTien = tongTien } });
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTimeSlots(int sanId, string ngayChoi, int? excludeBookingId = null)
        {
            if (!HasManagerAccess()) return NotFound();
            
            if (!DateOnly.TryParse(ngayChoi, out var selectedDate))
                return Json(new { success = false, message = "Ngày không hợp lệ." });

            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.Now;
            var isPastDay = selectedDate < today;
            
            // Lấy tất cả các đơn đặt sân không bị hủy cho sân này vào ngày đó
            var blockedStatuses = new[] { "Hoàn thành", "Đã hoàn thành", "Đã hủy", "Đã huỷ", "Thất bại" };
            var bookedSlots = await _context.DonDatSan
                .Where(d => d.SanID == sanId
                    && d.NgayChoi == selectedDate
                    && (!excludeBookingId.HasValue || d.DonDatSanID != excludeBookingId.Value)
                    && d.TrangThaiDon != null
                    && !blockedStatuses.Contains(d.TrangThaiDon.Trim()))
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
                // Slot quá khứ:
                // - Nếu ngày đã qua => khóa toàn bộ
                // - Nếu hôm nay => khóa nếu thời điểm bắt đầu slot <= hiện tại
                bool isPassed = isPastDay
                    || (selectedDate == today && new DateTime(now.Year, now.Month, now.Day, hour, 0, 0) <= now);
                
                allSlots.Add(new
                {
                    hour = hour,
                    isOccupied = isOccupied,
                    isPassed = isPassed,
                    disabled = isOccupied || isPassed,
                    label = isOccupied ? "Đã đặt" : (isPassed ? (isPastDay ? "Quá ngày" : "Quá giờ") : "Có sẵn")
                });
            }

            var pricingRows = await _context.BangGiaKhungGio
                .AsNoTracking()
                .Where(x => x.SanID == sanId && !string.IsNullOrWhiteSpace(x.KhungGio) && x.GiaTien.HasValue)
                .Select(x => new
                {
                    x.KhungGio,
                    GiaTien = x.GiaTien
                })
                .ToListAsync();

            var priceByHour = new Dictionary<int, decimal>();
            foreach (var row in pricingRows)
            {
                if (!TryParseKhungGioHours(row.KhungGio, out var hours))
                {
                    continue;
                }

                foreach (var hour in hours)
                {
                    if (!priceByHour.ContainsKey(hour))
                    {
                        priceByHour[hour] = row.GiaTien!.Value;
                    }
                }
            }

            var defaultPricePerHour = await _context.SanPickleball
                .AsNoTracking()
                .Where(x => x.SanID == sanId)
                .Select(x => x.GiaCoBan ?? 0)
                .FirstOrDefaultAsync();

            return Json(new
            {
                success = true,
                data = new
                {
                    slots = allSlots,
                    defaultPricePerHour,
                    priceByHour
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> CheckInCourt([FromBody] ManagerCheckoutCourtRequest request)
        {
            if (!HasManagerAccess()) return NotFound();
            if (request == null || request.SanID <= 0 || request.BookingID <= 0) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var donDat = await _context.DonDatSan
                    .Include(x => x.ThanhToans)
                    .FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);

                if (donDat == null) return NotFound(new { success = false, message = "Không tìm thấy đơn đặt sân." });
                if (!string.Equals(donDat.TrangThaiDon?.Trim(), "Đã xác nhận", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { success = false, message = "Chỉ có thể Check-in đơn đã xác nhận." });

                // Đổi trạng thái sang Đang chơi
                donDat.TrangThaiDon = "Đang chơi";

                // Kiểm tra xem đã thanh toán đủ chưa
                decimal daThanhToan = donDat.ThanhToans?.Sum(t => t.SoTien) ?? 0;
                decimal tongTien = donDat.TongTien ?? 0;

                // Nếu mới thanh toán cọc (chưa đủ tổng tiền) -> Sinh thêm log Thu tiền mặt phần còn thiếu
                if (daThanhToan != tongTien)
                {
                    var payment = new ThanhToan
                    {
                        DonDatSanID = donDat.DonDatSanID,
                        PhuongThuc = daThanhToan < tongTien ? "Tiền mặt tại quầy (Thu thêm)" : "Hoàn tiền mặt",
                        // Nếu hoàn trả, (tongTien - daThanhToan) sẽ ra SỐ ÂM. 
                        // DB lưu số âm là hoàn toàn chuẩn xác để hàm Sum() tiền cộng lại khớp 100%!
                        SoTien = tongTien - daThanhToan,
                        MaGiaoDich = $"MGR-PAY-{donDat.DonDatSanID}-{DateTime.Now:yyyyMMddHHmmss}",
                        TrangThai = daThanhToan < tongTien ? "Đã thanh toán" : "Đã hoàn tiền",
                        NgayThanhToan = DateTime.Now
                    };
                    _context.ThanhToan.Add(payment);
                }

                _context.DonDatSan.Update(donDat);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Check-in thành công! Đã ghi nhận thu đủ tiền." });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi Check-in." });
            }
        }

        private bool HasManagerAccess()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            var role = HttpContext.Session.GetString("VaiTro");
            if (!userId.HasValue || string.IsNullOrWhiteSpace(role)) return false;
            return role.Equals("Manager", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private async Task AutoCheckoutExpiredConfirmedBookings()
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            var candidates = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .Where(d => d.TrangThaiDon != null
                    && d.TrangThaiDon.Trim() == "Đã xác nhận"
                    && d.NgayChoi.HasValue)
                .ToListAsync();

            if (!candidates.Any()) return;

            var changed = false;
            foreach (var don in candidates)
            {
                var ngayChoi = don.NgayChoi!.Value;
                var shouldCheckout = false;

                if (ngayChoi < today)
                {
                    shouldCheckout = true;
                }
                else if (ngayChoi == today && !string.IsNullOrWhiteSpace(don.KhungGio))
                {
                    var hours = don.KhungGio
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => int.TryParse(p.Trim(), out var h) ? h : -1)
                        .Where(h => h >= 0 && h <= 23)
                        .ToList();

                    if (hours.Any())
                    {
                        var endHour = hours.Max() + 1;
                        var endTime = now.Date.AddHours(endHour);
                        shouldCheckout = now >= endTime;
                    }
                }

                if (!shouldCheckout) continue;

                don.TrangThaiDon = "Hoàn thành";
                if (don.SanPickleball != null)
                {
                    don.SanPickleball.TrangThai = "Trống";
                    _context.SanPickleball.Update(don.SanPickleball);
                }

                var hasPayment = await _context.ThanhToan.AnyAsync(x => x.DonDatSanID == don.DonDatSanID);
                if (!hasPayment)
                {
                    _context.ThanhToan.Add(new ThanhToan
                    {
                        DonDatSanID = don.DonDatSanID,
                        PhuongThuc = "Tiền mặt tại quầy",
                        SoTien = don.TongTien ?? 0,
                        MaGiaoDich = $"AUTO-{don.DonDatSanID}-{DateTime.Now:yyyyMMddHHmmss}",
                        TrangThai = "Hoàn thành",
                        NgayThanhToan = DateTime.Now
                    });
                }

                _context.DonDatSan.Update(don);
                changed = true;
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
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

        private static bool TryParseKhungGioHours(string? khungGio, out List<int> hours)
        {
            hours = new List<int>();
            if (string.IsNullOrWhiteSpace(khungGio)) return false;

            var parsed = khungGio
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var hour) ? hour : -1)
                .Where(hour => hour >= 0)
                .Distinct()
                .OrderBy(hour => hour)
                .ToList();

            if (!parsed.Any()) return false;
            hours = parsed;
            return true;
        }
    }
}
