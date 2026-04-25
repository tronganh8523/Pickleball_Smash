using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers.User.San
{
    public class SanController : Controller
    {
        private readonly AppDbContext _context;

        public SanController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchQuery, string? loaiSan, string? mucGia)
        {
            var query = _context.SanPickleball
                .Include(s => s.HinhAnhSans)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
                query = query.Where(s => s.TenSan.Contains(searchQuery.Trim()));

            if (!string.IsNullOrWhiteSpace(loaiSan))
                query = query.Where(s => s.LoaiSan == loaiSan);

            if (!string.IsNullOrWhiteSpace(mucGia))
            {
                if (mucGia == "duoi100") query = query.Where(s => s.GiaCoBan < 100000);
                else if (mucGia == "tren100") query = query.Where(s => s.GiaCoBan >= 100000);
            }

            var courts = await query.ToListAsync();
            var today = DateOnly.FromDateTime(DateTime.Now);

            foreach (var san in courts)
            {
                var bookings = await _context.DonDatSan
                    .Where(d => d.SanID == san.SanID
                             && d.NgayChoi == today
                             && d.TrangThaiDon != "Đã hủy")
                    .ToListAsync();

                int soGioDaDat = 0;
                foreach (var b in bookings)
                {
                    if (!string.IsNullOrEmpty(b.KhungGio))
                        soGioDaDat += b.KhungGio.Split(',').Length;
                }

                if (soGioDaDat >= 17) san.TrangThai = "Bận";
                else san.TrangThai = "Trống";
            }

            return View(courts);
        }

        [HttpGet]
        public async Task<IActionResult> GetBookedSlots(int sanId, DateTime date)
        {
            var bookedHours = new List<int>();

            // LỖI 1 ĐÃ FIX: Phải tách DateOnly ra một biến riêng TRƯỚC KHI đưa vào LINQ
            DateOnly targetDate = DateOnly.FromDateTime(date);

            var bookings = await _context.DonDatSan
                .Where(b => b.SanID == sanId
                         && b.NgayChoi == targetDate
                         && b.TrangThaiDon != "Đã hủy")
                .ToListAsync();

            foreach (var b in bookings)
            {
                if (!string.IsNullOrEmpty(b.KhungGio))
                {
                    // LỖI 2 ĐÃ FIX: Dùng TryParse và Trim() để chống sập khi chuỗi có khoảng trắng
                    var hours = b.KhungGio.Split(',')
                                          .Select(h => int.TryParse(h.Trim(), out var val) ? val : -1)
                                          .Where(h => h != -1)
                                          .ToList();
                    bookedHours.AddRange(hours);
                }
            }
            return Json(bookedHours.Distinct());
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] BookingRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            if (request.SelectedHours == null || !request.SelectedHours.Any())
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 khung giờ" });

            string chuoiKhungGio = string.Join(",", request.SelectedHours.OrderBy(h => h));

            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            var donDat = new DonDatSan
            {
                NguoiDungID = userId.Value,
                SanID = request.SanID,
                NgayChoi = DateOnly.FromDateTime(request.NgayDat),
                KhungGio = chuoiKhungGio,
                TongTien = request.TongTien,
                TrangThaiDon = "Chờ thanh toán",
                NgayTao = DateTime.Now
            };

            _context.DonDatSan.Add(donDat);
            await _context.SaveChangesAsync();

            return Json(new { success = true, bookingIds = new[] { donDat.DonDatSanID } });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            if (request?.BookingIds == null || request.BookingIds.Count == 0)
            {
                return Json(new { success = false, message = "Danh sách đơn thanh toán không hợp lệ." });
            }

            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            var uniqueBookingIds = request.BookingIds.Distinct().ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var donDats = await _context.DonDatSan
                    .Where(d => uniqueBookingIds.Contains(d.DonDatSanID) && d.NguoiDungID == userId.Value)
                    .ToListAsync();

                if (!donDats.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn cần thanh toán." });
                }

                Voucher? voucher = null;
                decimal tongTienDon = donDats.Sum(x => x.TongTien ?? 0m);
                if (!string.IsNullOrWhiteSpace(request.VoucherCode))
                {
                    var voucherResult = await ValidateVoucherAsync(request.VoucherCode, donDats.Count, tongTienDon);
                    if (!voucherResult.IsValid)
                    {
                        return Json(new { success = false, message = voucherResult.Message });
                    }

                    voucher = voucherResult.Voucher;
                }

                foreach (var donDat in donDats)
                {
                    decimal soTienGiam = 0m;
                    if (voucher != null)
                    {
                        soTienGiam = CalculateDiscountAmount(voucher, donDat.TongTien ?? 0m);
                        donDat.VoucherID = voucher.VoucherID;
                    }

                    donDat.SoTienGiam = soTienGiam;
                    donDat.TongTien = Math.Max(0m, (donDat.TongTien ?? 0m) - soTienGiam);
                    donDat.TrangThaiDon = "Chờ xác nhận";

                    var hasPayment = await _context.ThanhToan.AnyAsync(x => x.DonDatSanID == donDat.DonDatSanID);
                    if (!hasPayment)
                    {
                        var payment = new ThanhToan
                        {
                            DonDatSanID = donDat.DonDatSanID,
                            PhuongThuc = "Online",
                            SoTien = donDat.TongTien ?? 0,
                            MaGiaoDich = $"USR-{donDat.DonDatSanID}-{DateTime.Now:yyyyMMddHHmmss}",
                            TrangThai = "Đã thanh toán",
                            NgayThanhToan = DateTime.Now
                        };

                        _context.ThanhToan.Add(payment);
                    }
                }

                if (voucher != null)
                {
                    voucher.SoLuongDaDung = (voucher.SoLuongDaDung ?? 0) + donDats.Count;
                    _context.Voucher.Update(voucher);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true });
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Không thể xác nhận thanh toán. Vui lòng thử lại." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ValidateVoucher([FromBody] ValidateVoucherRequest request)
        {
            if (request?.BookingIds == null || request.BookingIds.Count == 0)
            {
                return Json(new { success = false, message = "Không có đơn đặt sân để áp dụng voucher." });
            }

            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            if (string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã voucher." });
            }

            var bookingIds = request.BookingIds.Distinct().ToList();
            var donDats = await _context.DonDatSan
                .Where(d => bookingIds.Contains(d.DonDatSanID) && d.NguoiDungID == userId.Value)
                .ToListAsync();

            if (!donDats.Any())
            {
                return Json(new { success = false, message = "Không tìm thấy đơn cần áp voucher." });
            }

            var tongTienDon = donDats.Sum(x => x.TongTien ?? 0m);
            var voucherResult = await ValidateVoucherAsync(request.VoucherCode, donDats.Count, tongTienDon);
            if (!voucherResult.IsValid || voucherResult.Voucher == null)
            {
                return Json(new { success = false, message = voucherResult.Message });
            }

            var voucher = voucherResult.Voucher;
            decimal tongGiam = donDats.Sum(d => CalculateDiscountAmount(voucher, d.TongTien ?? 0m));
            decimal thanhTien = Math.Max(0m, tongTienDon - tongGiam);

            return Json(new
            {
                success = true,
                message = "Áp voucher thành công.",
                discountAmount = tongGiam,
                finalAmount = thanhTien,
                voucherCode = voucher.MaVoucher
            });
        }

        [HttpPost]
        public async Task<IActionResult> ValidateVoucherBeforeBooking([FromBody] ValidateVoucherBeforeBookingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.VoucherCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã voucher." });
            }

            if (request.TotalAmount <= 0)
            {
                return Json(new { success = false, message = "Số tiền không hợp lệ." });
            }

            if (request.BookingCount <= 0)
            {
                return Json(new { success = false, message = "Số lượng đơn không hợp lệ." });
            }

            var voucherResult = await ValidateVoucherAsync(request.VoucherCode, request.BookingCount, request.TotalAmount);
            if (!voucherResult.IsValid || voucherResult.Voucher == null)
            {
                return Json(new { success = false, message = voucherResult.Message });
            }

            var voucher = voucherResult.Voucher;
            decimal discountAmount = CalculateDiscountAmount(voucher, request.TotalAmount);
            decimal finalAmount = Math.Max(0m, request.TotalAmount - discountAmount);

            return Json(new
            {
                success = true,
                message = "Áp voucher thành công.",
                discountAmount = discountAmount,
                finalAmount = finalAmount,
                voucherCode = voucher.MaVoucher,
                voucherId = voucher.VoucherID
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized();

            // 1. Mở rộng danh sách các trạng thái hợp lệ để hiển thị trong lịch sử
            var validStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Hoàn thành", "Đã thanh toán" };

            var history = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .Include(d => d.ThanhToans) // 2. Thêm Include này để lấy dữ liệu từ bảng ThanhToan
                .Where(d => d.NguoiDungID == userId && d.TrangThaiDon != null && validStatuses.Contains(d.TrangThaiDon))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            var result = history.Select(d => new {
                maHoaDon = d.DonDatSanID.ToString("D3"),
                ngayThanhToan = d.ThanhToans != null && d.ThanhToans.Any()
                    ? d.ThanhToans.First().NgayThanhToan?.ToString("dd/MM/yyyy HH:mm")
                    : (d.NgayTao.HasValue ? d.NgayTao.Value.ToString("dd/MM/yyyy HH:mm") : ""),
                loaiSan = d.SanPickleball != null ? d.SanPickleball.LoaiSan : string.Empty,
                khungGio = FormatKhungGioHienThi(d.KhungGio),
                tongTien = d.TongTien,
                soTienGiam = d.SoTienGiam ?? 0, // Thêm dòng này để lấy tiền giảm giá cho Hóa đơn
                trangThai = d.TrangThaiDon
            }).ToList();

            return Json(result);
        }
        [HttpPost]
        public async Task<IActionResult> CancelPendingBookings([FromBody] List<int> bookingIds)
        {
            if (bookingIds == null || !bookingIds.Any()) return Json(new { success = false });

            var pendingBookings = await _context.DonDatSan
                .Where(d => bookingIds.Contains(d.DonDatSanID))
                .ToListAsync();

            foreach (var b in pendingBookings)
            {
                // Bạn có thể xóa hẳn khỏi DB (Remove) hoặc chuyển trạng thái thành "Đã hủy"
                // Ở đây chọn cách xóa hẳn để đỡ rác DB cho các đơn chưa từng thanh toán
                _context.DonDatSan.Remove(b);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        // =========================================================================
        // THUẬT TOÁN GỘP CHUỖI CẢI TIẾN CHỐNG CRASH
        // =========================================================================
        private string FormatKhungGioHienThi(string? khungGioStr)
        {
            if (string.IsNullOrEmpty(khungGioStr)) return "";

            var parts = khungGioStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Dùng int.TryParse để nếu chuỗi có bị lỗi định dạng thì web vẫn không sập
            var hours = parts.Select(p => int.TryParse(p.Trim(), out var h) ? h : -1)
                             .Where(h => h != -1)
                             .OrderBy(h => h)
                             .ToList();

            if (!hours.Any()) return "";

            var result = new List<string>();
            int start = hours[0];
            int end = hours[0] + 1;

            for (int i = 1; i < hours.Count; i++)
            {
                if (hours[i] == end)
                {
                    end = hours[i] + 1;
                }
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

        private async Task<(bool IsValid, string Message, Voucher? Voucher)> ValidateVoucherAsync(string voucherCode, int soLuongDon, decimal tongTienDon)
        {
            var code = voucherCode.Trim();
            var voucher = await _context.Voucher
                .FirstOrDefaultAsync(v => v.MaVoucher.ToLower() == code.ToLower());

            if (voucher == null)
            {
                return (false, "Voucher không tồn tại.", null);
            }

            var now = DateTime.Now;
            if (voucher.NgayBatDau.HasValue && now < voucher.NgayBatDau.Value)
            {
                return (false, "Voucher chưa đến thời gian sử dụng.", null);
            }

            if (voucher.NgayKetThuc.HasValue && now > voucher.NgayKetThuc.Value)
            {
                return (false, "Voucher đã hết hạn.", null);
            }

            if (!string.IsNullOrWhiteSpace(voucher.TrangThai) && voucher.TrangThai.Contains("hết", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Voucher đã hết hạn.", null);
            }

            var soLuongConLai = (voucher.SoLuongToiDa ?? int.MaxValue) - (voucher.SoLuongDaDung ?? 0);
            if (soLuongConLai < soLuongDon)
            {
                return (false, "Voucher đã hết lượt sử dụng.", null);
            }

            if ((voucher.GiaTriDonToiThieu ?? 0m) > tongTienDon)
            {
                return (false, "Đơn hàng chưa đạt giá trị tối thiểu để dùng voucher.", null);
            }

            if (voucher.GiaTriDonToiThieu.HasValue && tongTienDon < voucher.GiaTriDonToiThieu.Value)
            {
                return (false, "Tổng tiền đơn hàng không đạt giá trị tối thiểu để áp dụng voucher.", null);
            }

            if (voucher.SoLuongToiDa.HasValue && voucher.SoLuongDaDung >= voucher.SoLuongToiDa)
            {
                return (false, "Voucher đã được sử dụng hết.", null);
            }

            return (true, string.Empty, voucher);
        }

        private static decimal CalculateDiscountAmount(Voucher voucher, decimal amount)
        {
            if (amount <= 0) return 0m;

            decimal giam = 0m;
            if (string.Equals(voucher.LoaiGiamGia, "%", StringComparison.OrdinalIgnoreCase))
            {
                giam = amount * ((voucher.GiaTriGiam ?? 0m) / 100m);
            }
            else
            {
                giam = voucher.GiaTriGiam ?? 0m;
            }

            if (voucher.GiamToiDa.HasValue && voucher.GiamToiDa.Value > 0)
            {
                giam = Math.Min(giam, voucher.GiamToiDa.Value);
            }

            return Math.Max(0m, Math.Min(giam, amount));
        }
    }

    public class BookingRequest
    {
        public int SanID { get; set; }
        public DateTime NgayDat { get; set; }
        public List<int> SelectedHours { get; set; } = new();
        public string? GhiChu { get; set; }
        public decimal TongTien { get; set; }
    }

    public class ConfirmPaymentRequest
    {
        public List<int> BookingIds { get; set; } = new();
        public string? VoucherCode { get; set; }
    }

    public class ValidateVoucherRequest
    {
        public List<int> BookingIds { get; set; } = new();
        public string? VoucherCode { get; set; }
    }

    public class ValidateVoucherBeforeBookingRequest
    {
        public string? VoucherCode { get; set; }
        public decimal TotalAmount { get; set; }
        public int BookingCount { get; set; }
    }
}