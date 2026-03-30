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

        public async Task<IActionResult> Dashboard(string? loaiSan, string? trangThai)
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

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

            ViewBag.LoaiSanOptions = loaiSanOptions;
            ViewBag.TrangThaiOptions = trangThaiOptions;
            ViewBag.SelectedLoaiSan = loaiSan;
            ViewBag.SelectedTrangThai = trangThai;

            return View("~/Views/Manager/Dashboard.cshtml", model);
        }

        public async Task<IActionResult> Bookings()
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            var donDat = await _context.DonDatSan
                .AsNoTracking()
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            return View("~/Views/Manager/Bookings.cshtml", donDat);
        }

        public async Task<IActionResult> History()
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            var lichSu = await _context.DonDatSan
                .AsNoTracking()
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .Where(d =>
                    string.Equals(d.TrangThaiDon, "Hoàn thành", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(d.TrangThaiDon, "Đã hủy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(d.TrangThaiDon, "Thất bại", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            return View("~/Views/Manager/History.cshtml", lichSu);
        }

        public async Task<IActionResult> Profile()
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            var manager = await _context.NguoiDung
                .AsNoTracking()
                .OrderByDescending(x => x.NgayTao)
                .FirstOrDefaultAsync(x => x.VaiTro != null && x.VaiTro.ToLower() == "manager");

            return View("~/Views/Manager/Profile.cshtml", manager);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] ManagerCreateBookingRequest request)
        {
            if (!HasManagerAccess())
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu đặt sân không hợp lệ." });
            }

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

            if (!TimeOnly.TryParse(request.GioBatDau, out var gioBatDau)
                || !TimeOnly.TryParse(request.GioKetThuc, out var gioKetThuc))
            {
                return BadRequest(new { success = false, message = "Khung giờ không hợp lệ." });
            }

            if (gioKetThuc <= gioBatDau)
            {
                return BadRequest(new { success = false, message = "Giờ kết thúc phải lớn hơn giờ bắt đầu." });
            }

            var san = await _context.SanPickleball.FirstOrDefaultAsync(x => x.SanID == request.SanID);
            if (san == null)
            {
                return BadRequest(new { success = false, message = "Sân không tồn tại." });
            }

            var trangThaiDonDangHoatDong = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chơi", "Đã đặt" };
            var donCungNgay = await _context.DonDatSan
                .AsNoTracking()
                .Where(x =>
                    x.SanID == request.SanID
                    && x.NgayChoi == ngayChoi
                    && x.TrangThaiDon != null
                    && trangThaiDonDangHoatDong.Contains(x.TrangThaiDon))
                .ToListAsync();

            var biTrungGio = donCungNgay.Any(x =>
                x.ThoiGianBatDau.HasValue
                && x.ThoiGianKetThuc.HasValue
                && gioBatDau < x.ThoiGianKetThuc.Value
                && gioKetThuc > x.ThoiGianBatDau.Value);

            if (biTrungGio)
            {
                return BadRequest(new { success = false, message = "Sân đã có lịch trong khung giờ này. Vui lòng chọn giờ khác." });
            }

            var tongGio = (decimal)(gioKetThuc.ToTimeSpan() - gioBatDau.ToTimeSpan()).TotalHours;
            var tongTien = (san.GiaCoBan ?? 0) * tongGio;

            var nguoiDung = await _context.NguoiDung
                .FirstOrDefaultAsync(x => x.SDT != null && x.SDT == soDienThoai);

            if (nguoiDung == null)
            {
                var baseUsername = $"kh{new string(soDienThoai.Where(char.IsDigit).ToArray())}";
                if (string.IsNullOrWhiteSpace(baseUsername) || baseUsername.Length < 4)
                {
                    baseUsername = $"kh{DateTime.Now:yyyyMMddHHmmss}";
                }

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
                ThoiGianBatDau = gioBatDau,
                ThoiGianKetThuc = gioKetThuc,
                TongTien = tongTien,
                SoTienGiam = 0,
                TrangThaiDon = "Chờ xác nhận",
                NgayTao = DateTime.Now
            };

            san.TrangThai = "Bận";

            _context.DonDatSan.Add(donDat);
            _context.SanPickleball.Update(san);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đặt sân thành công.",
                bookingId = donDat.DonDatSanID
            });
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
                .FirstOrDefaultAsync(x => x.DonDatSanID == request.BookingID && x.SanID == request.SanID);

            if (donDat == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đơn đang hoạt động của sân." });
            }

            donDat.TrangThaiDon = "Hoàn thành";

            var san = await _context.SanPickleball.FirstOrDefaultAsync(x => x.SanID == request.SanID);
            if (san != null)
            {
                san.TrangThai = "Trống";
                _context.SanPickleball.Update(san);
            }

            _context.DonDatSan.Update(donDat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã check-out và giải phóng sân." });
        }

        private static ManagerCourtCardViewModel BuildCourtCard(SanPickleball san, DonDatSan? donDangHoatDong)
        {
            var status = donDangHoatDong != null ? "Bận" : san.TrangThai?.Trim() ?? "Trống";

            var card = new ManagerCourtCardViewModel
            {
                SanID = san.SanID,
                BookingDangHoatDongID = donDangHoatDong?.DonDatSanID,
                TenSan = san.TenSan,
                LoaiSan = string.IsNullOrWhiteSpace(san.LoaiSan) ? "Chưa cập nhật" : san.LoaiSan,
                GiaCoBan = san.GiaCoBan ?? 0,
                TinhTrang = status,
                MoTaNgan = string.IsNullOrWhiteSpace(san.MoTa)
                    ? "Sân đạt tiêu chuẩn thi đấu, phù hợp cho mọi trình độ."
                    : san.MoTa!
            };

            if (status.Contains("bận", StringComparison.OrdinalIgnoreCase)
                || status.Contains("đã đặt", StringComparison.OrdinalIgnoreCase)
                || status.Contains("hủy", StringComparison.OrdinalIgnoreCase)
                || status.Contains("thất bại", StringComparison.OrdinalIgnoreCase))
            {
                card.BadgeClass = "status-busy";
                card.ActionClass = "btn-checkout";
                card.ActionText = "Check-out";
            }
            else if (status.Contains("chờ", StringComparison.OrdinalIgnoreCase))
            {
                card.BadgeClass = "status-busy";
                card.ActionClass = "btn-checkout";
                card.ActionText = "Check-out";
            }

            return card;
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
    }
}
