using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Pickleball_Smash.Models.ViewModels;
using System.Text.RegularExpressions;

namespace Pickleball_Smash.Controllers.User
{
    public class UserController : Controller
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Danh Sách Sân
        public async Task<IActionResult> DanhSachSan()
        {
            var sans = await _context.SanPickleball
                .Include(s => s.ChiNhanh)
                .ToListAsync();

            var thongKeDanhGia = await _context.DanhGia
                .Where(dg => dg.SanID.HasValue)
                .GroupBy(dg => dg.SanID!.Value)
                .Select(g => new
                {
                    SanID = g.Key,
                    SoLuongDanhGia = g.Count(),
                    DiemTrungBinh = Math.Round(g.Average(x => x.SoSao ?? 0), 1)
                })
                .ToListAsync();

            ViewBag.DiemTrungBinhTheoSan = thongKeDanhGia.ToDictionary(x => x.SanID, x => x.DiemTrungBinh);
            ViewBag.SoLuongDanhGiaTheoSan = thongKeDanhGia.ToDictionary(x => x.SanID, x => x.SoLuongDanhGia);

            ViewBag.DanhSachChiNhanh = sans
                .Where(s => !string.IsNullOrWhiteSpace(s.ChiNhanh?.TenChiNhanh))
                .Select(s => s.ChiNhanh!.TenChiNhanh!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            ViewBag.DanhSachLoaiSan = sans
                .Where(s => !string.IsNullOrWhiteSpace(s.LoaiSan))
                .Select(s => s.LoaiSan!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            return View("~/Views/User/DanhSachSan.cshtml", sans);
        }

        // GET: Chi Tiết Sân
        public async Task<IActionResult> ChiTietSan(int id)
        {
            var san = await _context.SanPickleball
                .Include(s => s.ChiNhanh)
                .FirstOrDefaultAsync(s => s.SanID == id);

            if (san == null)
                return NotFound();

            var danhGias = await _context.DanhGia
                .Include(dg => dg.NguoiDung)
                .Where(dg => dg.SanID == id)
                .OrderByDescending(dg => dg.NgayDanhGia)
                .ToListAsync();

            var viewModel = new UserSanChiTietViewModel
            {
                San = san,
                DanhGias = danhGias,
                SoLuongDanhGia = danhGias.Count,
                DiemTrungBinh = danhGias.Count == 0
                    ? 0
                    : Math.Round(danhGias.Average(dg => dg.SoSao ?? 0), 1)
            };

            return View("~/Views/User/ChiTietSan.cshtml", viewModel);
        }

        // GET: Form Đặt Sân
        public async Task<IActionResult> DatSan(int id)
        {
            var san = await _context.SanPickleball
                .Include(s => s.ChiNhanh)
                .FirstOrDefaultAsync(s => s.SanID == id);

            if (san == null)
                return NotFound();

            return View("~/Views/User/DatSan.cshtml", san);
        }

        // POST: Đặt Sân
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DatSan(int sanId, DateOnly ngayChoi, int gioBatDau, int gioKetThuc)
        {
            var san = await _context.SanPickleball.FindAsync(sanId);
            if (san == null)
                return NotFound();

            if (gioBatDau < 6 || gioBatDau > 23)
            {
                TempData["Error"] = "Giờ bắt đầu phải trong khoảng 06:00 - 23:00.";
                return RedirectToAction(nameof(DatSan), new { id = sanId });
            }

            if (gioKetThuc < 7 || gioKetThuc > 24)
            {
                TempData["Error"] = "Giờ kết thúc phải trong khoảng 07:00 - 24:00.";
                return RedirectToAction(nameof(DatSan), new { id = sanId });
            }

            var trangThaiSan = san.TrangThai?.Trim() ?? string.Empty;
            var isMoCua = trangThaiSan.Equals("Mở", StringComparison.OrdinalIgnoreCase)
                || trangThaiSan.Equals("Hoạt động", StringComparison.OrdinalIgnoreCase)
                || trangThaiSan.Equals("Open", StringComparison.OrdinalIgnoreCase);

            if (!isMoCua)
            {
                TempData["Error"] = "Sân hiện không mở cửa để đặt.";
                return RedirectToAction(nameof(ChiTietSan), new { id = sanId });
            }

            if (gioKetThuc - gioBatDau < 1)
            {
                TempData["Error"] = "Giờ kết thúc phải lớn hơn giờ bắt đầu tối thiểu 1 giờ.";
                return RedirectToAction(nameof(DatSan), new { id = sanId });
            }

            var soGioDat = gioKetThuc - gioBatDau;

            if (soGioDat <= 0)
            {
                TempData["Error"] = "Thời lượng đặt sân không hợp lệ.";
                return RedirectToAction(nameof(DatSan), new { id = sanId });
            }

            var tongTien = (san.GiaCoBan ?? 0) * soGioDat;

            var thoiGianBatDau = new TimeOnly(gioBatDau, 0);
            var thoiGianKetThuc = gioKetThuc == 24
                ? new TimeOnly(0, 0)
                : new TimeOnly(gioKetThuc, 0);

            var batDauPhutMoi = gioBatDau * 60;
            var ketThucPhutMoi = gioKetThuc * 60;

            var isTrungLich = await _context.DonDatSan.AnyAsync(d =>
                d.SanID == sanId
                && d.NgayChoi == ngayChoi
                && d.TrangThaiDon != "Đã huỷ"
                && d.ThoiGianBatDau.HasValue
                && d.ThoiGianKetThuc.HasValue
                && batDauPhutMoi < ((d.ThoiGianKetThuc.Value.Hour == 0 ? 24 : d.ThoiGianKetThuc.Value.Hour) * 60)
                && ketThucPhutMoi > (d.ThoiGianBatDau.Value.Hour * 60));

            if (isTrungLich)
            {
                TempData["Error"] = "Khung giờ này đã có người đặt. Vui lòng chọn giờ khác.";
                return RedirectToAction(nameof(DatSan), new { id = sanId });
            }

            // Tạo đơn đặt sân mới
            var donDat = new Pickleball_Smash.Models.DonDatSan
            {
                SanID = sanId,
                NgayChoi = ngayChoi,
                ThoiGianBatDau = thoiGianBatDau,
                ThoiGianKetThuc = thoiGianKetThuc,
                TongTien = tongTien,
                SoTienGiam = 0,
                TrangThaiDon = "Chờ xác nhận",
                NgayTao = DateTime.Now
            };

            _context.DonDatSan.Add(donDat);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đặt sân thành công! Vui lòng chờ xác nhận.";
            return RedirectToAction(nameof(DanhSachSan));
        }

        // GET: Giỏ Hàng
        public IActionResult GioHang()
        {
            return View("~/Views/User/GioHang.cshtml");
        }

        // GET: Tài Khoản
        public async Task<IActionResult> TaiKhoan()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var user = await _context.NguoiDung.FirstOrDefaultAsync(x => x.NguoiDungID == userId.Value);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            return View("~/Views/User/TaiKhoan.cshtml", user);
        }

        // POST: Cập nhật hồ sơ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatHoSo([Bind("HoTen,Email,SDT,GioiTinh")] NguoiDung form)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var user = await _context.NguoiDung.FirstOrDefaultAsync(x => x.NguoiDungID == userId.Value);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            var email = form.Email?.Trim() ?? string.Empty;
            var phone = form.SDT?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorProfile"] = "Email là bắt buộc.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            {
                TempData["ErrorProfile"] = "Email không đúng định dạng.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            var emailExists = await _context.NguoiDung.AnyAsync(x =>
                x.NguoiDungID != user.NguoiDungID
                && x.Email != null
                && x.Email.ToLower() == email.ToLower());

            if (emailExists)
            {
                TempData["ErrorProfile"] = "Email đã tồn tại.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneExists = await _context.NguoiDung.AnyAsync(x =>
                    x.NguoiDungID != user.NguoiDungID
                    && x.SDT != null
                    && x.SDT == phone);

                if (phoneExists)
                {
                    TempData["ErrorProfile"] = "Số điện thoại đã tồn tại.";
                    return RedirectToAction(nameof(TaiKhoan));
                }
            }

            user.HoTen = form.HoTen?.Trim();
            user.Email = email;
            user.SDT = phone;
            user.GioiTinh = form.GioiTinh?.Trim();

            await _context.SaveChangesAsync();
            TempData["SuccessProfile"] = "Cập nhật hồ sơ thành công.";

            return RedirectToAction(nameof(TaiKhoan));
        }

        // POST: Đổi mật khẩu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiMatKhau(string matKhauHienTai, string matKhauMoi, string xacNhanMatKhauMoi)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var user = await _context.NguoiDung.FirstOrDefaultAsync(x => x.NguoiDungID == userId.Value);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(matKhauHienTai) || string.IsNullOrWhiteSpace(matKhauMoi) || string.IsNullOrWhiteSpace(xacNhanMatKhauMoi))
            {
                TempData["ErrorPassword"] = "Vui lòng nhập đầy đủ thông tin mật khẩu.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            if (!VerifyPassword(matKhauHienTai, user.MatKhau))
            {
                TempData["ErrorPassword"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            if (matKhauMoi != xacNhanMatKhauMoi)
            {
                TempData["ErrorPassword"] = "Xác nhận mật khẩu mới không khớp.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            if (!IsValidPassword(matKhauMoi))
            {
                TempData["ErrorPassword"] = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số.";
                return RedirectToAction(nameof(TaiKhoan));
            }

            user.MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauMoi);
            await _context.SaveChangesAsync();
            TempData["SuccessPassword"] = "Đổi mật khẩu thành công.";

            return RedirectToAction(nameof(TaiKhoan));
        }

        // GET: Lịch Sử Đặt Sân
        public async Task<IActionResult> LichSuDatSan()
        {
            var tatCaDon = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .ToListAsync();
            return View("~/Views/User/LichSuDatSan.cshtml", tatCaDon);
        }

        private static bool VerifyPassword(string plainTextPassword, string storedPassword)
        {
            if (string.IsNullOrWhiteSpace(storedPassword))
            {
                return false;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(plainTextPassword, storedPassword);
            }
            catch
            {
                return plainTextPassword == storedPassword;
            }
        }

        private static bool IsValidPassword(string password)
        {
            if (password.Length < 8)
            {
                return false;
            }

            var hasLower = Regex.IsMatch(password, "[a-z]");
            var hasUpper = Regex.IsMatch(password, "[A-Z]");
            var hasDigit = Regex.IsMatch(password, "[0-9]");

            return hasLower && hasUpper && hasDigit;
        }
    }
}
