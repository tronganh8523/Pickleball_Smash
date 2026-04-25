using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class ManagerHistoryController : Controller
    {
        private readonly AppDbContext _context;

        public ManagerHistoryController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> History(int? sanId, string? khungGio, string? ngayChoiTu, string? ngayChoiDen, string? trangThai, string? sapXep, string? tuKhoa)
        {
            if (!HasManagerAccess()) return Forbid();

            var historyStatuses = new[] { "Hoàn thành", "Đã hủy", "Thất bại" };

            var allHistory = await _context.DonDatSan
                .AsNoTracking()
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .Include(d => d.Voucher)
                .Include(d => d.ThanhToans)
                .Where(d => d.TrangThaiDon != null && historyStatuses.Contains(d.TrangThaiDon))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            var sanOptions = allHistory
                .Where(d => d.SanPickleball != null)
                .Select(d => new { Id = d.SanID ?? 0, TenSan = d.SanPickleball!.TenSan ?? "N/A" })
                .Where(x => x.Id > 0)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.TenSan)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.TenSan })
                .ToList();

            var khungGioOptions = allHistory
                .Select(FormatBookingTimeRange)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "--:--")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(label => new SelectListItem { Value = label, Text = label })
                .ToList();

            var trangThaiOptions = allHistory
                .Select(d => NormalizeStatus(d.TrangThaiDon))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new SelectListItem { Value = x, Text = x })
                .ToList();

            var filtered = allHistory.AsEnumerable();

            if (sanId.HasValue && sanId.Value > 0)
                filtered = filtered.Where(d => d.SanID == sanId.Value);

            if (!string.IsNullOrWhiteSpace(khungGio))
                filtered = filtered.Where(d => string.Equals(FormatBookingTimeRange(d), khungGio.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(ngayChoiTu) && DateOnly.TryParse(ngayChoiTu, out var fromDate))
                filtered = filtered.Where(d => d.NgayChoi.HasValue && d.NgayChoi.Value >= fromDate);

            if (!string.IsNullOrWhiteSpace(ngayChoiDen) && DateOnly.TryParse(ngayChoiDen, out var toDate))
                filtered = filtered.Where(d => d.NgayChoi.HasValue && d.NgayChoi.Value <= toDate);

            if (!string.IsNullOrWhiteSpace(trangThai))
                filtered = filtered.Where(d => string.Equals(NormalizeStatus(d.TrangThaiDon), trangThai.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var keyword = tuKhoa.Trim();
                filtered = filtered.Where(d =>
                    (!string.IsNullOrWhiteSpace(d.NguoiDung?.HoTen) && d.NguoiDung.HoTen.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(d.NguoiDung?.TenDangNhap) && d.NguoiDung.TenDangNhap.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(d.NguoiDung?.SDT) && d.NguoiDung.SDT.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            filtered = (sapXep ?? "created_desc").Trim().ToLowerInvariant() switch
            {
                "created_asc" => filtered.OrderBy(d => d.NgayTao),
                "playdate_desc" => filtered.OrderByDescending(d => d.NgayChoi),
                "playdate_asc" => filtered.OrderBy(d => d.NgayChoi),
                "total_desc" => filtered.OrderByDescending(d => d.TongTien ?? 0),
                "total_asc" => filtered.OrderBy(d => d.TongTien ?? 0),
                _ => filtered.OrderByDescending(d => d.NgayTao)
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

            return View("~/Views/Manager/History.cshtml", filtered.ToList());
        }

        [HttpGet]
        public async Task<IActionResult> HistoryDetail(int id)
        {
            if (!HasManagerAccess()) return Forbid();

            var donDat = await _context.DonDatSan
                .AsNoTracking()
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .Include(d => d.Voucher)
                .Include(d => d.ThanhToans)
                .FirstOrDefaultAsync(d => d.DonDatSanID == id);

            if (donDat == null) return NotFound(new { success = false, message = "Không tìm thấy đơn lịch sử." });

            var payments = (donDat.ThanhToans ?? Enumerable.Empty<ThanhToan>())
                .OrderByDescending(x => x.NgayThanhToan)
                .Select(x => new
                {
                    x.ThanhToanID,
                    x.PhuongThuc,
                    x.SoTien,
                    x.MaGiaoDich,
                    x.TrangThai,
                    NgayThanhToan = x.NgayThanhToan?.ToString("dd/MM/yyyy HH:mm")
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    donDat.DonDatSanID,
                    khachHang = donDat.NguoiDung?.HoTen ?? donDat.NguoiDung?.TenDangNhap ?? "Khách lẻ",
                    soDienThoai = donDat.NguoiDung?.SDT ?? "-",
                    san = donDat.SanPickleball?.TenSan ?? "N/A",
                    ngayChoi = donDat.NgayChoi?.ToString("dd/MM/yyyy") ?? "-",
                    khungGio = FormatBookingTimeRange(donDat),
                    tongTien = (donDat.TongTien ?? 0).ToString("N0"),
                    soTienGiam = (donDat.SoTienGiam ?? 0).ToString("N0"),
                    trangThai = NormalizeStatus(donDat.TrangThaiDon),
                    ngayTao = donDat.NgayTao?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                    voucher = donDat.Voucher?.MaVoucher,
                    voucherMoTa = donDat.Voucher?.MoTa,
                    voucherGiam = donDat.Voucher?.GiaTriGiam?.ToString("N0") ?? "0",
                    payments
                }
            });
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