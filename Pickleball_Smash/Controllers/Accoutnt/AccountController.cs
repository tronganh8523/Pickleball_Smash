using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });
            }

            var user = await _context.NguoiDung.FirstOrDefaultAsync(u =>
                (u.TenDangNhap == request.Username || u.Email == request.Username || u.SDT == request.Username)
                && u.MatKhau == request.Password
                && u.VaiTro == request.Role);

            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không đúng" });
            }

            HttpContext.Session.SetInt32("UserID", user.NguoiDungID);
            HttpContext.Session.SetString("HoTen", user.HoTen ?? user.TenDangNhap);
            HttpContext.Session.SetString("VaiTro", user.VaiTro ?? "KhachHang");

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên đăng nhập và mật khẩu." });
            }

            // Kiểm tra trùng lặp (Username, Email, SDT)
            if (await _context.NguoiDung.AnyAsync(u => u.TenDangNhap == request.Username))
            {
                return Json(new { success = false, message = "Tên đăng nhập đã tồn tại." });
            }
            if (!string.IsNullOrWhiteSpace(request.Email) && await _context.NguoiDung.AnyAsync(u => u.Email == request.Email))
            {
                return Json(new { success = false, message = "Email đã được sử dụng." });
            }
            if (!string.IsNullOrWhiteSpace(request.Phone) && await _context.NguoiDung.AnyAsync(u => u.SDT == request.Phone))
            {
                return Json(new { success = false, message = "Số điện thoại đã được sử dụng." });
            }

            // Tạo người dùng mới với đầy đủ thông tin
            var newUser = new NguoiDung
            {
                TenDangNhap = request.Username,
                MatKhau = request.Password,
                HoTen = request.FullName,
                Email = request.Email,
                SDT = request.Phone,
                GioiTinh = request.Gender,
                VaiTro = "KhachHang",
                NgayTao = DateTime.Now
            };

            // Nếu người dùng có chọn Ngày sinh
            if (DateTime.TryParse(request.Dob, out DateTime parsedDate))
            {
                newUser.NgaySinh = parsedDate;
            }

            _context.NguoiDung.Add(newUser);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }


        // API 1: Lấy thông tin cá nhân
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized();

            var user = await _context.NguoiDung.FindAsync(userId);
            if (user == null) return NotFound();

            return Json(new
            {
                hoTen = user.HoTen,
                email = user.Email,
                sdt = user.SDT,
                // Kiểm tra xem bảng NguoiDung của bạn có cột NgaySinh và GioiTinh chưa, nếu chưa có hãy thêm vào nhé
                ngaySinh = user.NgaySinh?.ToString("yyyy-MM-dd"),
                gioiTinh = user.GioiTinh ?? "Nam",
                maKhachHang = "ID" + user.NguoiDungID.ToString("D4")
            });
        }

        // API 2: Cập nhật thông tin cá nhân
        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var user = await _context.NguoiDung.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy người dùng" });

            // Kiểm tra mật khẩu cũ nếu người dùng muốn đổi mật khẩu
            if (!string.IsNullOrEmpty(request.NewPassword))
            {
                if (user.MatKhau != request.OldPassword)
                    return Json(new { success = false, message = "Mật khẩu cũ không chính xác!" });

                user.MatKhau = request.NewPassword;
            }

            // Cập nhật các thông tin khác
            user.HoTen = request.FullName;
            user.Email = request.Email;
            user.SDT = request.Phone;
            user.GioiTinh = request.Gender;
            if (DateTime.TryParse(request.Dob, out DateTime parsedDate))
            {
                user.NgaySinh = parsedDate; // Hoặc DateOnly tùy cấu trúc DB của bạn
            }

            await _context.SaveChangesAsync();

            // Cập nhật lại Session tên người dùng
            HttpContext.Session.SetString("HoTen", user.HoTen ?? user.TenDangNhap);

            return Json(new { success = true, message = "Cập nhật thành công!" });
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Role { get; set; }
        }

        public class RegisterRequest
        {
            public string Username { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Dob { get; set; }
            public string Gender { get; set; }
            public string Password { get; set; }
        }

        public class UpdateProfileRequest
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Dob { get; set; }
            public string Gender { get; set; }
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
        }
    }
}