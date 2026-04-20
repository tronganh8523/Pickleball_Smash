using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Pickleball_Smash.Models.ViewModels;

namespace Pickleball_Smash.Controllers
{
    public class ManagerController : Controller
    {
        private readonly AppDbContext _context;

        public ManagerController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> Dashboard(string? tenSan, string? loaiSan, string? trangThai)
        {
            if (!HasManagerAccess()) return Forbid();

            tenSan = string.IsNullOrWhiteSpace(tenSan) ? null : tenSan.Trim();
            loaiSan = string.IsNullOrWhiteSpace(loaiSan) ? null : loaiSan.Trim();
            trangThai = string.IsNullOrWhiteSpace(trangThai) ? null : trangThai.Trim();

            var homNay = DateOnly.FromDateTime(DateTime.Today);

            var tatCaSan = await _context.SanPickleball
                .AsNoTracking()
                .OrderBy(s => s.SanID)
                .ToListAsync();

            var tatCaDon = await _context.DonDatSan
                .AsNoTracking()
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            var anhDauTienTheoSan = await _context.HinhAnhSan
                .AsNoTracking()
                .Where(x => x.SanID != null && !string.IsNullOrWhiteSpace(x.DuongDanURL))
                .GroupBy(x => x.SanID!.Value)
                .Select(g => new
                {
                    SanID = g.Key,
                    Anh = g.Select(x => x.DuongDanURL).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.SanID, x => x.Anh ?? string.Empty);

            var trangThaiDonDangHoatDong = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chơi", "Đã đặt" };
            var donDangHoatDongTheoSan = tatCaDon
                .Where(d =>
                    d.SanID.HasValue
                    && !string.IsNullOrWhiteSpace(d.TrangThaiDon)
                    && trangThaiDonDangHoatDong.Contains(d.TrangThaiDon!))
                .GroupBy(d => d.SanID!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var model = new ManagerDashboardViewModel
            {
                TongSan = tatCaSan.Count,
                TongSanConTrong = tatCaSan.Count(s => string.Equals(s.TrangThai, "Trống", StringComparison.OrdinalIgnoreCase)),
                TongDonHomNay = tatCaDon.Count(d => d.NgayChoi == homNay),
                TongDonChoXacNhan = tatCaDon.Count(d => string.Equals(d.TrangThaiDon, "Chờ xác nhận", StringComparison.OrdinalIgnoreCase)),
                TongSanDangBan = tatCaSan.Count(s => string.Equals(s.TrangThai, "Bận", StringComparison.OrdinalIgnoreCase)),
                DonGanDay = await _context.DonDatSan
                    .AsNoTracking()
                    .Include(d => d.SanPickleball)
                    .Include(d => d.NguoiDung)
                    .OrderByDescending(d => d.NgayTao)
                    .Take(6)
                    .ToListAsync()
            };

            foreach (var san in tatCaSan)
            {
                var donDangHoatDong = donDangHoatDongTheoSan.TryGetValue(san.SanID, out var don) ? don : null;
                var card = BuildCourtCard(san, donDangHoatDong);

                if (anhDauTienTheoSan.TryGetValue(san.SanID, out var anh) && !string.IsNullOrWhiteSpace(anh))
                {
                    card.AnhDaiDienUrl = anh;
                }

                model.DanhSachSan.Add(card);
            }

            var loaiSanOptions = model.DanhSachSan
                .Select(x => x.LoaiSan)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var trangThaiOptions = model.DanhSachSan
                .Select(x => x.TinhTrang)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (!string.IsNullOrWhiteSpace(tenSan))
            {
                model.DanhSachSan = model.DanhSachSan
                    .Where(x => x.TenSan.Contains(tenSan, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(loaiSan))
            {
                model.DanhSachSan = model.DanhSachSan
                    .Where(x => string.Equals(x.LoaiSan, loaiSan, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                model.DanhSachSan = model.DanhSachSan
                    .Where(x => string.Equals(x.TinhTrang, trangThai, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.SelectedTenSan = tenSan;
            ViewBag.LoaiSanOptions = loaiSanOptions;
            ViewBag.TrangThaiOptions = trangThaiOptions;
            ViewBag.SelectedLoaiSan = loaiSan;
            ViewBag.SelectedTrangThai = trangThai;

            if (Request.Headers.TryGetValue("X-Requested-With", out var requestedWith)
                && string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PartialView("~/Views/Manager/_CourtGrid.cshtml", model);
            }

            return View("~/Views/Manager/Dashboard.cshtml", model);
        }

        public async Task<IActionResult> Profile()
        {
            if (!HasManagerAccess()) return Forbid();

            var manager = await _context.NguoiDung
                .AsNoTracking()
                .OrderByDescending(x => x.NgayTao)
                .FirstOrDefaultAsync(x => x.VaiTro != null && x.VaiTro.ToLower() == "manager");

            return View("~/Views/Manager/Profile.cshtml", manager);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] ManagerCreateBookingRequest request)
        {
            if (!HasManagerAccess()) return Forbid();

            if (request == null) return BadRequest(new { success = false, message = "Dữ liệu đặt sân không hợp lệ." });

            var tenKhach = request.TenKhachHang?.Trim() ?? string.Empty;
            var soDienThoai = request.SoDienThoai?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tenKhach) || string.IsNullOrWhiteSpace(soDienThoai))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập tên khách hàng và số điện thoại." });
            }

            if (!DateOnly.TryParse(request.NgayChoi, out var ngayChoi))
            {
                return BadRequest(new { success = false, message = "Ngày chơi không hợp lệ." });
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (ngayChoi < today)
            {
                return BadRequest(new { success = false, message = "Không thể đặt sân cho ngày trong quá khứ." });
            }

            if (!TryParseBookingTime(request.GioBatDau, out var gioBatDau, out var gioBatDauIsEndOfDay)
                || !TryParseBookingTime(request.GioKetThuc, out var gioKetThuc, out var gioKetThucIsEndOfDay))
            {
                return BadRequest(new { success = false, message = "Khung giờ không hợp lệ." });
            }

            if (gioBatDauIsEndOfDay || (!gioKetThucIsEndOfDay && gioKetThuc <= gioBatDau))
            {
                return BadRequest(new { success = false, message = "Giờ kết thúc phải lớn hơn giờ bắt đầu." });
            }

            var bookingStartHour = GetBookingMinute(gioBatDau, gioBatDauIsEndOfDay) / 60;
            var bookingEndHour = GetBookingMinute(gioKetThuc, gioKetThucIsEndOfDay) / 60;

            var san = await _context.SanPickleball.FirstOrDefaultAsync(x => x.SanID == request.SanID);
            if (san == null) return BadRequest(new { success = false, message = "Sân không tồn tại." });

            if (!string.Equals(san.TrangThai?.Trim(), "Trống", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = $"Sân hiện đang có trạng thái '{san.TrangThai}' và không thể đặt. Vui lòng chọn sân khác." });
            }

            // KIỂM TRA TRÙNG LẶP DỰA TRÊN CHUỖI KhungGio
            var trangThaiDonDangHoatDong = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chơi", "Đã đặt" };
            var donCungNgay = await _context.DonDatSan
                .AsNoTracking()
                .Where(x => x.SanID == request.SanID && x.NgayChoi == ngayChoi && x.TrangThaiDon != null && trangThaiDonDangHoatDong.Contains(x.TrangThaiDon))
                .ToListAsync();

            var newHours = Enumerable.Range(bookingStartHour, bookingEndHour - bookingStartHour).ToList();

            var biTrungGio = donCungNgay.Any(x =>
            {
                if (string.IsNullOrEmpty(x.KhungGio)) return false;
                var existingHours = x.KhungGio.Split(',').Select(h => int.TryParse(h.Trim(), out var parsed) ? parsed : -1).Where(h => h != -1);
                return newHours.Intersect(existingHours).Any();
            });

            if (biTrungGio)
            {
                return BadRequest(new { success = false, message = "Sân đã có lịch trong khung giờ này. Vui lòng chọn giờ khác." });
            }

            // TÍNH TIỀN
            var bangGiaKhungGio = await _context.BangGiaKhungGio
                .AsNoTracking()
                .Where(x => x.SanID == san.SanID && x.GioBatDau.HasValue && x.GioKetThuc.HasValue)
                .Select(x => new
                {
                    StartHour = x.GioBatDau!.Value.Hour,
                    EndHour = x.GioKetThuc!.Value == TimeOnly.MinValue ? 24 : x.GioKetThuc.Value.Hour,
                    GiaTien = x.GiaTien
                })
                .ToListAsync();

            decimal tongTien = 0;
            var giaCoBan = san.GiaCoBan ?? 0;

            for (var hour = bookingStartHour; hour < bookingEndHour; hour++)
            {
                var giaTheoKhung = bangGiaKhungGio.FirstOrDefault(x => x.StartHour <= hour && hour < x.EndHour && x.GiaTien.HasValue)?.GiaTien;
                var donGia = giaTheoKhung ?? giaCoBan;
                if (donGia <= 0) return BadRequest(new { success = false, message = "Sân chưa được cấu hình giá hợp lệ cho khung giờ đã chọn." });
                tongTien += donGia;
            }

            var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(x => x.SDT != null && x.SDT == soDienThoai);

            if (nguoiDung == null)
            {
                var baseUsername = $"kh{new string(soDienThoai.Where(char.IsDigit).ToArray())}";
                if (string.IsNullOrWhiteSpace(baseUsername) || baseUsername.Length < 4) baseUsername = $"kh{DateTime.Now:yyyyMMddHHmmss}";

                var username = baseUsername;
                var suffix = 1;
                while (await _context.NguoiDung.AnyAsync(x => x.TenDangNhap == username))
                {
                    username = $"{baseUsername}{suffix}";
                    suffix++;
                }

                nguoiDung = new NguoiDung
                {
                    TenDangNhap = username,
                    MatKhau = BCrypt.Net.BCrypt.HashPassword("User@123"),
                    HoTen = tenKhach,
                    SDT = soDienThoai,
                    VaiTro = "User",
                    NgayTao = DateTime.Now
                };

                _context.NguoiDung.Add(nguoiDung);
                await _context.SaveChangesAsync();
            }
            else if (string.IsNullOrWhiteSpace(nguoiDung.HoTen))
            {
                nguoiDung.HoTen = tenKhach;
                _context.NguoiDung.Update(nguoiDung);
                await _context.SaveChangesAsync();
            }

            var donDat = new DonDatSan
            {
                NguoiDungID = nguoiDung.NguoiDungID,
                SanID = san.SanID,
                NgayChoi = ngayChoi,
                KhungGio = string.Join(",", newHours), // LƯU KHUNG GIỜ VÀO DATABASE
                TongTien = tongTien,
                SoTienGiam = 0,
                TrangThaiDon = "Chờ xác nhận",
                NgayTao = DateTime.Now
            };

            _context.DonDatSan.Add(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đặt sân thành công.", bookingId = donDat.DonDatSanID });
        }

        [HttpGet]
        public async Task<IActionResult> GetCourtBookingsForDay(int sanId, string? ngayChoi)
        {
            if (!HasManagerAccess()) return Forbid();
            if (!DateOnly.TryParse(ngayChoi, out var date)) return BadRequest(new { success = false, message = "Ngày không hợp lệ." });

            var trangThaiDonDangHoatDong = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chơi", "Đã đặt" };

            var rawBookings = await _context.DonDatSan
                .AsNoTracking()
                .Include(x => x.NguoiDung)
                .Where(x => x.SanID == sanId && x.NgayChoi == date && x.TrangThaiDon != null && trangThaiDonDangHoatDong.Contains(x.TrangThaiDon))
                .ToListAsync();

            var bookings = rawBookings
                // Sắp xếp theo giờ bắt đầu sớm nhất
                .OrderBy(x => string.IsNullOrEmpty(x.KhungGio) ? 99 : x.KhungGio.Split(',').Select(int.Parse).FirstOrDefault())
                .Select(x => new
                {
                    x.DonDatSanID,
                    khachHang = x.NguoiDung != null ? (x.NguoiDung.HoTen ?? x.NguoiDung.TenDangNhap ?? "Khách lẻ") : "Khách lẻ",
                    soDienThoai = x.NguoiDung != null ? x.NguoiDung.SDT : "-",
                    // Gắn khung giờ gộp vào biến UI cũ để không bị lỗi giao diện Javascript
                    gioBatDau = FormatBookingTimeRange(x),
                    gioKetThuc = "",
                    tongTien = (x.TongTien ?? 0).ToString("N0"),
                    trangThai = x.TrangThaiDon ?? "-"
                })
                .ToList();

            var pricingRanges = await _context.BangGiaKhungGio
                .AsNoTracking()
                .Where(x => x.SanID == sanId && x.GioBatDau.HasValue && x.GioKetThuc.HasValue && x.GiaTien.HasValue)
                .Select(x => new
                {
                    startHour = x.GioBatDau!.Value.Hour,
                    endHour = x.GioKetThuc!.Value == TimeOnly.MinValue ? 24 : x.GioKetThuc.Value.Hour,
                    giaTien = x.GiaTien!.Value
                })
                .ToListAsync();

            return Ok(new { success = true, bookings = bookings, pricingRanges = pricingRanges });
        }

        [HttpGet]
        public async Task<IActionResult> LookupCustomerByPhone(string? soDienThoai)
        {
            if (!HasManagerAccess()) return Forbid();

            var phone = soDienThoai?.Trim();
            if (string.IsNullOrWhiteSpace(phone)) return Ok(new { found = false, hoTen = string.Empty });

            var matchedName = await _context.DonDatSan
                .AsNoTracking()
                .Where(x => x.NguoiDung != null && x.NguoiDung.SDT != null && x.NguoiDung.SDT == phone && x.NguoiDung.HoTen != null && x.NguoiDung.HoTen.Trim() != string.Empty)
                .OrderByDescending(x => x.NgayTao)
                .Select(x => x.NguoiDung!.HoTen!)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(matchedName)) return Ok(new { found = false, hoTen = string.Empty });

            return Ok(new { found = true, hoTen = matchedName.Trim() });
        }

        // ================= HÀM HỖ TRỢ FORMAT KHUNG GIỜ =================
        private static string FormatBookingTimeRange(DonDatSan booking)
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

        private static ManagerCourtCardViewModel BuildCourtCard(SanPickleball san, DonDatSan? donDangHoatDong)
        {
            var status = san.TrangThai?.Trim() ?? "Trống";
            var isAvailable = status.Contains("trống", StringComparison.OrdinalIgnoreCase);

            var card = new ManagerCourtCardViewModel
            {
                SanID = san.SanID,
                BookingDangHoatDongID = donDangHoatDong?.DonDatSanID,
                CanBook = isAvailable,
                TenSan = san.TenSan,
                LoaiSan = string.IsNullOrWhiteSpace(san.LoaiSan) ? "Chưa cập nhật" : san.LoaiSan,
                GiaCoBan = san.GiaCoBan ?? 0,
                TinhTrang = status,
                MoTaNgan = string.IsNullOrWhiteSpace(san.MoTa) ? "Sân đạt tiêu chuẩn thi đấu, phù hợp cho mọi trình độ." : san.MoTa!
            };

            if (!isAvailable) card.BadgeClass = "status-busy";

            return card;
        }

        private bool HasManagerAccess()
        {
            var role = HttpContext.Session.GetString("VaiTro");
            if (string.IsNullOrWhiteSpace(role)) return true;
            return role.Equals("Manager", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseBookingTime(string? value, out TimeOnly timeOnly, out bool isEndOfDay)
        {
            isEndOfDay = false;
            timeOnly = default;
            var normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return false;
            if (string.Equals(normalized, "24:00", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "24:00:00", StringComparison.OrdinalIgnoreCase))
            {
                timeOnly = TimeOnly.MinValue;
                isEndOfDay = true;
                return true;
            }
            return TimeOnly.TryParse(normalized, out timeOnly);
        }

        private static int GetBookingMinute(TimeOnly timeOnly, bool isEndOfDay)
        {
            return isEndOfDay || timeOnly == TimeOnly.MinValue ? 24 * 60 : timeOnly.Hour * 60 + timeOnly.Minute;
        }
    }
}