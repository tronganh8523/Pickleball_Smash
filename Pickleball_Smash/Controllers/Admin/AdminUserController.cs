using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Filters;
using Pickleball_Smash.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pickleball_Smash.Controllers
{
    [AdminAuthorize]
    public class AdminUserController : Controller
    {
        private readonly AppDbContext _context;

        public AdminUserController(AppDbContext context)
        {
            _context = context;
        }

        // GET: User - List
        public async Task<IActionResult> Index()
        {
            var items = await _context.NguoiDung
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync();
            return View("~/Views/Admin/User/Index.cshtml", items);
        }

        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập." });

            var user = await _context.NguoiDung.FindAsync(userId.Value);
            if (user == null) return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

            return Ok(new
            {
                success = true,
                data = new
                {
                    user.HoTen,
                    user.Email,
                    user.SDT,
                    user.GioiTinh
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập." });

            var user = await _context.NguoiDung.FindAsync(userId.Value);
            if (user == null) return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

            var email = request.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                return BadRequest(new { success = false, message = "Email không hợp lệ." });

            if (await IsEmailExists(email, user.NguoiDungID))
                return BadRequest(new { success = false, message = "Email đã tồn tại." });

            if (!string.IsNullOrWhiteSpace(request.Phone) && await IsPhoneExists(request.Phone.Trim(), user.NguoiDungID))
                return BadRequest(new { success = false, message = "Số điện thoại đã tồn tại." });

            user.HoTen = request.FullName?.Trim();
            user.Email = email;
            user.SDT = request.Phone?.Trim();
            user.GioiTinh = request.Gender?.Trim();

            await _context.SaveChangesAsync();
            HttpContext.Session.SetString("HoTen", user.HoTen ?? user.TenDangNhap);

            return Ok(new { success = true, message = "Cập nhật thông tin cá nhân thành công." });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập." });

            if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ mật khẩu cũ, mật khẩu mới và xác nhận mật khẩu." });

            if (request.NewPassword.Length < 8)
                return BadRequest(new { success = false, message = "Mật khẩu mới phải có ít nhất 8 ký tự." });

            if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
                return BadRequest(new { success = false, message = "Mật khẩu xác nhận không khớp." });

            var user = await _context.NguoiDung.FindAsync(userId.Value);
            if (user == null) return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

            var isOldPasswordCorrect = !string.IsNullOrWhiteSpace(user.MatKhau) && user.MatKhau.StartsWith("$2", StringComparison.Ordinal)
                ? BCrypt.Net.BCrypt.Verify(request.OldPassword, user.MatKhau)
                : string.Equals(user.MatKhau, request.OldPassword, StringComparison.Ordinal);

            if (!isOldPasswordCorrect)
                return BadRequest(new { success = false, message = "Mật khẩu cũ không chính xác." });

            user.MatKhau = (!string.IsNullOrWhiteSpace(user.MatKhau) && user.MatKhau.StartsWith("$2", StringComparison.Ordinal))
                ? BCrypt.Net.BCrypt.HashPassword(request.NewPassword)
                : request.NewPassword;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đổi mật khẩu thành công." });
        }

        // GET: User - Edit
        public IActionResult Edit(int? id)
        {
            TempData["Error"] = "Vui lòng thao tác chỉnh sửa tài khoản bằng popup tại trang danh sách.";
            return RedirectToAction(nameof(Index), "AdminUser");
        }

        // POST: User - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("NguoiDungID,TenDangNhap,MatKhau,Email,HoTen,GioiTinh,SDT,VaiTro,NgayTao")] NguoiDung nguoiDung)
        {
            if (id != nguoiDung.NguoiDungID) return NotFound();

            nguoiDung.TenDangNhap = nguoiDung.TenDangNhap?.Trim() ?? string.Empty;
            nguoiDung.Email = nguoiDung.Email?.Trim();
            nguoiDung.SDT = nguoiDung.SDT?.Trim();
            ValidateUser(nguoiDung);

            if (await IsUsernameExists(nguoiDung.TenDangNhap, nguoiDung.NguoiDungID))
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(nguoiDung.Email) && await IsEmailExists(nguoiDung.Email, nguoiDung.NguoiDungID))
            {
                ModelState.AddModelError("Email", "Email đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(nguoiDung.SDT) && await IsPhoneExists(nguoiDung.SDT, nguoiDung.NguoiDungID))
            {
                ModelState.AddModelError("SDT", "Số điện thoại đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    nguoiDung.MatKhau = BCrypt.Net.BCrypt.HashPassword(nguoiDung.MatKhau);
                    _context.Update(nguoiDung);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật tài khoản thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.NguoiDung.Any(e => e.NguoiDungID == nguoiDung.NguoiDungID))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index), "AdminUser");
            }

            SetModalState("edit-user", nguoiDung);
            return RedirectToAction(nameof(Index), "AdminUser");
        }

        // GET: User - Delete
        public IActionResult Delete(int? id)
        {
            TempData["Error"] = "Chức năng xóa trực tiếp bằng popup, không dùng trang Delete riêng.";
            return RedirectToAction(nameof(Index), "AdminUser");
        }

        // POST: User - Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nguoiDung = await _context.NguoiDung.FindAsync(id);
            if (nguoiDung != null)
            {
                var hasBooking = await _context.DonDatSan.AnyAsync(x => x.NguoiDungID == id);
                var hasReview = await _context.DanhGia.AnyAsync(x => x.NguoiDungID == id);
                var hasChat = await _context.LichSuChat.AnyAsync(x => x.NguoiDungID == id);

                if (hasBooking || hasReview || hasChat)
                {
                    TempData["Error"] = "Không thể xóa tài khoản vì đang có dữ liệu liên quan.";
                    return RedirectToAction(nameof(Index), "AdminUser");
                }

                _context.NguoiDung.Remove(nguoiDung);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa tài khoản thành công!";
            }

            return RedirectToAction(nameof(Index), "AdminUser");
        }

        private void ValidateUser(NguoiDung nguoiDung)
        {
            var tenDangNhap = nguoiDung.TenDangNhap?.Trim() ?? string.Empty;
            var matKhau = nguoiDung.MatKhau ?? string.Empty;
            var email = nguoiDung.Email?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tenDangNhap))
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập là bắt buộc.");
            }
            else if (tenDangNhap.Length < 6)
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập phải có ít nhất 6 ký tự.");
            }

            if (string.IsNullOrWhiteSpace(matKhau))
            {
                ModelState.AddModelError("MatKhau", "Mật khẩu là bắt buộc.");
            }
            else
            {
                if (matKhau.Length < 8)
                {
                    ModelState.AddModelError("MatKhau", "Mật khẩu phải có ít nhất 8 ký tự.");
                }

                if (!Regex.IsMatch(matKhau, "[a-z]"))
                {
                    ModelState.AddModelError("MatKhau", "Mật khẩu phải có ít nhất 1 chữ thường.");
                }

                if (!Regex.IsMatch(matKhau, "[A-Z]"))
                {
                    ModelState.AddModelError("MatKhau", "Mật khẩu phải có ít nhất 1 chữ hoa.");
                }

                if (!Regex.IsMatch(matKhau, "[0-9]"))
                {
                    ModelState.AddModelError("MatKhau", "Mật khẩu phải có ít nhất 1 chữ số.");
                }
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("Email", "Email là bắt buộc.");
            }
            else if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            {
                ModelState.AddModelError("Email", "Email không đúng định dạng.");
            }

            if (string.IsNullOrWhiteSpace(nguoiDung.VaiTro))
            {
                ModelState.AddModelError("VaiTro", "Vai trò là bắt buộc.");
            }
            else if (!string.Equals(nguoiDung.VaiTro, "Admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(nguoiDung.VaiTro, "Manager", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(nguoiDung.VaiTro, "User", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("VaiTro", "Vai trò chỉ được chọn Admin, Manager hoặc User.");
            }
        }

        private async Task<bool> IsUsernameExists(string username, int? excludeUserId = null)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            var normalized = username.Trim().ToLower();
            return await _context.NguoiDung.AnyAsync(x =>
                x.TenDangNhap.ToLower() == normalized
                && (!excludeUserId.HasValue || x.NguoiDungID != excludeUserId.Value));
        }

        private async Task<bool> IsEmailExists(string email, int? excludeUserId = null)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            var normalized = email.Trim().ToLower();
            return await _context.NguoiDung.AnyAsync(x =>
                x.Email != null
                && x.Email.ToLower() == normalized
                && (!excludeUserId.HasValue || x.NguoiDungID != excludeUserId.Value));
        }

        private async Task<bool> IsPhoneExists(string phone, int? excludeUserId = null)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;

            var normalized = phone.Trim();
            return await _context.NguoiDung.AnyAsync(x =>
                x.SDT != null
                && x.SDT == normalized
                && (!excludeUserId.HasValue || x.NguoiDungID != excludeUserId.Value));
        }

        private void SetModalState(string openModal, object modalData)
        {
            TempData["OpenModal"] = openModal;
            TempData["ModalData"] = JsonSerializer.Serialize(modalData);
            TempData["ModalErrors"] = string.Join("\n", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());
        }

        public class UpdateMyProfileRequest
        {
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? Gender { get; set; }
        }

        public class ChangeMyPasswordRequest
        {
            public string OldPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
            public string ConfirmPassword { get; set; } = string.Empty;
        }
    }
}
