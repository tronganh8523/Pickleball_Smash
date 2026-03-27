using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using System.Text.RegularExpressions;

namespace Pickleball_Smash.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Role")))
            {
                return RedirectByRole(HttpContext.Session.GetString("Role")!);
            }

            return View("~/Views/Auth/Login.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string tenDangNhap, string matKhau)
        {
            tenDangNhap = tenDangNhap?.Trim() ?? string.Empty;
            matKhau = matKhau ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tenDangNhap) || string.IsNullOrWhiteSpace(matKhau))
            {
                ViewBag.Error = "Vui lòng nhập tên đăng nhập và mật khẩu.";
                return View("~/Views/Auth/Login.cshtml");
            }

            var user = await _context.NguoiDung
                .FirstOrDefaultAsync(x => x.TenDangNhap.ToLower() == tenDangNhap.ToLower());

            if (user == null)
            {
                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return View("~/Views/Auth/Login.cshtml");
            }

            var isValid = VerifyPassword(matKhau, user.MatKhau);
            if (!isValid)
            {
                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return View("~/Views/Auth/Login.cshtml");
            }

            var role = NormalizeRole(user.VaiTro);
            if (role is null)
            {
                ViewBag.Error = "Tài khoản chưa được gán vai trò hợp lệ (Admin/User).";
                return View("~/Views/Auth/Login.cshtml");
            }

            HttpContext.Session.SetInt32("UserId", user.NguoiDungID);
            HttpContext.Session.SetString("Username", user.TenDangNhap);
            HttpContext.Session.SetString("Role", role);

            return RedirectByRole(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult LogoutAndGoHome()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AdminChangePassword()
        {
            var role = HttpContext.Session.GetString("Role");
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Login));
            }

            return View("~/Views/Auth/AdminChangePassword.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminChangePassword(string matKhauHienTai, string matKhauMoi, string xacNhanMatKhauMoi)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) || !userId.HasValue)
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _context.NguoiDung.FirstOrDefaultAsync(x => x.NguoiDungID == userId.Value);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            if (string.IsNullOrWhiteSpace(matKhauHienTai)
                || string.IsNullOrWhiteSpace(matKhauMoi)
                || string.IsNullOrWhiteSpace(xacNhanMatKhauMoi))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin mật khẩu.";
                return RedirectToAction(nameof(AdminChangePassword));
            }

            if (!VerifyPassword(matKhauHienTai, user.MatKhau))
            {
                TempData["Error"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction(nameof(AdminChangePassword));
            }

            if (matKhauMoi != xacNhanMatKhauMoi)
            {
                TempData["Error"] = "Xác nhận mật khẩu mới không khớp.";
                return RedirectToAction(nameof(AdminChangePassword));
            }

            if (!IsValidPassword(matKhauMoi))
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số.";
                return RedirectToAction(nameof(AdminChangePassword));
            }

            user.MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauMoi);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(AdminChangePassword));
        }

        private IActionResult RedirectByRole(string role)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            return RedirectToAction("Index", "Home");
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
                // Backward compatibility for old plaintext records.
                return plainTextPassword == storedPassword;
            }
        }

        private static string? NormalizeRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return null;
            }

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            {
                return "User";
            }

            return null;
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
