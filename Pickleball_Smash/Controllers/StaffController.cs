using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Pickleball_Smash.Models.ViewModels;

namespace Pickleball_Smash.Controllers
{
    public class StaffController : Controller
    {
        private readonly AppDbContext _context;

        public StaffController(AppDbContext context)
        {
            _context = context;
        }

        // Trang chủ nhân viên
        public async Task<IActionResult> Index(string? searchQuery, string? loaiSan, string? trangThai)
        {
            // (Tuỳ chọn) Lấy tên nhân viên từ Session. Giả sử biến session lưu tên là "HoTen"
            var hoTen = HttpContext.Session.GetString("HoTen") ?? "Nhân viên";

            var query = _context.SanPickleball
                .AsNoTracking()
                .Include(s => s.HinhAnhSans)
                .AsQueryable();

            // 1. Lọc theo tên sân (Search)
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(s => s.TenSan.Contains(searchQuery.Trim()));
            }

            // 2. Lọc theo Loại sân
            if (!string.IsNullOrWhiteSpace(loaiSan))
            {
                query = query.Where(s => s.LoaiSan == loaiSan);
            }

            // 3. Lọc theo Trạng thái
            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                query = query.Where(s => s.TrangThai == trangThai);
            }

            var sanEntities = await query.ToListAsync();

            var model = new StaffDashboardViewModel
            {
                HoTenNhanVien = hoTen,
                SearchQuery = searchQuery,
                SelectedLoaiSan = loaiSan,
                SelectedTrangThai = trangThai
            };

            foreach (var san in sanEntities)
            {
                var anhDauTien = san.HinhAnhSans?.FirstOrDefault()?.DuongDanURL;
                model.DanhSachSan.Add(new StaffCourtCardViewModel
                {
                    SanID = san.SanID,
                    TenSan = san.TenSan,
                    LoaiSan = san.LoaiSan ?? "Chưa cập nhật",
                    TrangThai = san.TrangThai ?? "Trống",
                    AnhDaiDienUrl = !string.IsNullOrWhiteSpace(anhDauTien) ? anhDauTien : "/Img/SanMau1.jpg"
                });
            }

            return View(model);
        }

        // API Hỗ trợ Autocomplete Gợi ý tìm kiếm
        [HttpGet]
        public async Task<IActionResult> SuggestCourts(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return Json(new List<string>());

            var suggestions = await _context.SanPickleball
                .Where(s => s.TenSan.Contains(term))
                .Select(s => s.TenSan)
                .Take(5) // Lấy tối đa 5 gợi ý
                .ToListAsync();

            return Json(suggestions);
        }

        // Hàm này dùng để TẠO TỰ ĐỘNG 8 SÂN MẪU vào DB (Chạy 1 lần rồi có thể xoá hoặc comment lại)
        // Truy cập URL: /Staff/SeedData để tạo
        public async Task<IActionResult> SeedData()
        {
            if (await _context.SanPickleball.AnyAsync(s => s.TenSan.StartsWith("San")))
            {
                return Content("Dữ liệu sân đã tồn tại. Không cần seed lại.");
            }

            for (int i = 1; i <= 8; i++)
            {
                var loaiSan = (i <= 4) ? "Trong nhà" : "Ngoài trời";
                var trangThai = (i % 2 != 0) ? "Bận" : "Trống"; // Lẻ (1,3,5,7) là Bận, Chẵn (2,4,6,8) là Trống
                var extension = (i == 8) ? ".png" : ".jpg"; // Xử lý riêng ảnh 8 là .png theo yêu cầu của bạn

                var san = new SanPickleball
                {
                    TenSan = $"San {i}",
                    LoaiSan = loaiSan,
                    TrangThai = trangThai,
                    GiaCoBan = 100000,
                    MoTa = $"Đây là sân {i} dành cho Pickleball"
                };

                _context.SanPickleball.Add(san);
                await _context.SaveChangesAsync(); // Lưu để có SanID

                // Gán ảnh mẫu cho sân
                _context.HinhAnhSan.Add(new HinhAnhSan
                {
                    SanID = san.SanID,
                    DuongDanURL = $"/Img/SanMau{i}{extension}"
                });
            }

            await _context.SaveChangesAsync();
            return Content("Đã tạo 8 sân mẫu thành công! Bạn có thể quay lại trang /Staff");
        }
    }
}