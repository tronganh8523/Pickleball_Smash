using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;

namespace Pickleball_Smash.Controllers
{
    [Route("wp-admin")]
    public class AdminAuthController : Controller
    {
        private readonly AppDbContext _context;

        public AdminAuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public IActionResult Login([FromQuery] string? returnUrl = null)
        {
            var role = HttpContext.Session.GetString("VaiTro");
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View("~/Views/Admin/Auth/Login.cshtml");
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPost([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var loginValue = username.Trim();
            var admin = await _context.NguoiDung.FirstOrDefaultAsync(u =>
                (u.TenDangNhap == loginValue || u.Email == loginValue || u.SDT == loginValue)
                && u.VaiTro != null
                && u.VaiTro == "Admin");

            if (admin == null)
            {
                TempData["Error"] = "Tài khoản admin không tồn tại.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var passwordValid = false;
            if (!string.IsNullOrWhiteSpace(admin.MatKhau) && admin.MatKhau.StartsWith("$2", StringComparison.Ordinal))
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(password, admin.MatKhau);
            }
            else
            {
                passwordValid = string.Equals(admin.MatKhau, password, StringComparison.Ordinal);
            }

            if (!passwordValid)
            {
                TempData["Error"] = "Mật khẩu không chính xác.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            HttpContext.Session.SetInt32("UserID", admin.NguoiDungID);
            HttpContext.Session.SetString("HoTen", admin.HoTen ?? admin.TenDangNhap);
            HttpContext.Session.SetString("VaiTro", "Admin");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Dashboard", "Admin");
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }
    }
}
