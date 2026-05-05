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
                // 1. Nếu Admin đã set trạng thái là Bận (hoặc Bảo trì) trong DB, thì giữ nguyên và bỏ qua việc đếm giờ
                if (san.TrangThai == "Bận" || san.TrangThai == "Bảo trì" || san.TrangThai == "Ngừng hoạt động")
                {
                    continue; // Chuyển sang kiểm tra sân tiếp theo
                }

                // 2. Nếu Admin để là "Trống", thì mới bắt đầu tính toán tự động xem hôm nay khách đã đặt full lịch chưa
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

                // Nếu hôm nay khách đã đặt kín 17 tiếng (từ 5h-22h), tự động chuyển sang Bận
                if (soGioDaDat >= 17)
                {
                    san.TrangThai = "Bận";
                }
                // Không cần hàm 'else san.TrangThai = "Trống"' nữa vì DB mặc định đã là Trống rồi
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

        [HttpGet]
        public async Task<IActionResult> GetCourtCustomPrices(int sanId)
        {
            // Lấy danh sách các giá tùy chỉnh của sân này
            var customPrices = await _context.BangGiaKhungGio
                .Where(p => p.SanID == sanId)
                .ToListAsync();

            // Chuyển đổi thành dạng Dictionary { "12": 200000, "5": 100000 } để Javascript dễ đọc
            var dict = new Dictionary<string, decimal>();
            foreach (var item in customPrices)
            {
                // Chú ý: Đảm bảo class Model BangGiaKhungGio.cs của bạn có thuộc tính KhungGio (int)
                if (item.KhungGio != null)
                {
                    dict[item.KhungGio.ToString()] = item.GiaTien ?? 0m;
                }
            }

            return Json(dict);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] BookingRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            if (request.SelectedHours == null || !request.SelectedHours.Any())
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 khung giờ" });

            // BẮT ĐẦU TRANSACTION: Xếp hàng xử lý để chống đụng độ (Race Condition)
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                DateOnly targetDate = DateOnly.FromDateTime(request.NgayDat);

                // BƯỚC 1: KIỂM TRA TRÙNG LẶP (OVERLAP CHECK)
                // Kéo tất cả các đơn của sân này trong ngày hôm đó (Trừ các đơn đã hủy/hoàn tiền/thất bại)
                var existingBookings = await _context.DonDatSan
                    .Where(b => b.SanID == request.SanID
                             && b.NgayChoi == targetDate
                             && b.TrangThaiDon != "Đã hủy"
                             && b.TrangThaiDon != "Đã hoàn tiền"
                             && b.TrangThaiDon != "Thất bại")
                    .ToListAsync();

                // Gom tất cả các khung giờ đã có người đặt vào 1 danh sách
                var occupiedHours = new List<int>();
                foreach (var b in existingBookings)
                {
                    if (!string.IsNullOrEmpty(b.KhungGio))
                    {
                        var hours = b.KhungGio.Split(',')
                                              .Select(h => int.TryParse(h.Trim(), out var val) ? val : -1)
                                              .Where(h => h >= 0);
                        occupiedHours.AddRange(hours);
                    }
                }

                // Dùng phép giao (Intersect) xem giờ khách đang chọn có dính vào giờ đã bị đặt không
                var conflicts = request.SelectedHours.Intersect(occupiedHours).ToList();
                if (conflicts.Any())
                {
                    // Nếu có đụng độ -> Hủy bỏ ngay lập tức và báo lỗi
                    return Json(new { success = false, message = "Rất tiếc! Khung giờ này vừa có người khác nhanh tay đặt mất. Vui lòng chọn giờ khác nhé." });
                }

                // BƯỚC 2: NẾU AN TOÀN -> TIẾN HÀNH LƯU VÀO DATABASE
                string chuoiKhungGio = string.Join(",", request.SelectedHours.OrderBy(h => h));

                var donDat = new DonDatSan
                {
                    NguoiDungID = userId.Value,
                    SanID = request.SanID,
                    NgayChoi = targetDate,
                    KhungGio = chuoiKhungGio,
                    TongTien = request.TongTien,
                    TrangThaiDon = "Chờ thanh toán",
                    NgayTao = DateTime.Now
                };

                _context.DonDatSan.Add(donDat);
                await _context.SaveChangesAsync();

                // BƯỚC 3: XÁC NHẬN TRANSACTION THÀNH CÔNG
                await transaction.CommitAsync();

                return Json(new { success = true, bookingIds = new[] { donDat.DonDatSanID } });
            }
            catch (Exception ex)
            {
                // Rút lại toàn bộ thao tác nếu xảy ra lỗi Database
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi đặt sân. Vui lòng thử lại!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RevokeEditRequest([FromBody] CancelRequestModel request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            var don = await _context.DonDatSan.FirstOrDefaultAsync(d => d.DonDatSanID == request.DonDatSanID && d.NguoiDungID == userId);
            if (don == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

            // Tắt cờ yêu cầu sửa và làm sạch nội dung ghi chú
            don.YeuCauSua = false;
            don.NoiDungSua = null;

            _context.DonDatSan.Update(don);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        [HttpGet]
        public async Task<IActionResult> GetAllCourts()
        {
            // Lấy toàn bộ sân đang hoạt động để đổ vào Dropdown Đặt sân
            var courts = await _context.SanPickleball
                .Where(s => s.TrangThai != "Bảo trì" && s.TrangThai != "Ngừng hoạt động")
                .Select(s => new {
                    sanId = s.SanID,
                    tenSan = s.TenSan,
                    loaiSan = s.LoaiSan,
                    giaCoBan = s.GiaCoBan
                })
                .ToListAsync();

            return Json(courts);
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

                    // Trạng thái đơn chuyển sang chờ quản lý xác nhận
                    donDat.TrangThaiDon = "Chờ xác nhận";

                    var hasPayment = await _context.ThanhToan.AnyAsync(x => x.DonDatSanID == donDat.DonDatSanID);
                    if (!hasPayment)
                    {
                        // ========================================================
                        // LOGIC XỬ LÝ SỐ TIỀN THANH TOÁN (50% HOẶC 100%) Ở ĐÂY
                        // ========================================================
                        decimal tienThanhToanThucTe = donDat.TongTien ?? 0m;

                        // Kiểm tra nếu khách hàng chọn Đặt Cọc 50% thì chia đôi số tiền
                        if (request.PaymentType == "Coc50")
                        {
                            tienThanhToanThucTe = tienThanhToanThucTe / 2m;
                        }

                        var payment = new ThanhToan
                        {
                            DonDatSanID = donDat.DonDatSanID,
                            PhuongThuc = "Online",
                            // Ghi nhận đúng số tiền khách đã chuyển (50% hoặc 100%) vào DB
                            SoTien = tienThanhToanThucTe,
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

            var validStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chơi", "Hoàn thành", "Đã thanh toán", "Đã hủy", "Đã hoàn tiền" };

            // Lấy lịch sử đơn đặt sân
            // 1. Thêm Include(d => d.NguoiDung) để lấy thông tin khách hàng
            var history = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .Include(d => d.ThanhToans)
                .Include(d => d.NguoiDung)
                .Where(d => d.NguoiDungID == userId && d.TrangThaiDon != null && validStatuses.Contains(d.TrangThaiDon))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            // Lấy danh sách tất cả đánh giá của user này
            var userReviews = await _context.DanhGia
                .Where(r => r.NguoiDungID == userId)
                .ToListAsync();

            var result = history.Select(d => {
                var review = userReviews
        .FirstOrDefault(r => r.DonDatSanID == d.DonDatSanID);

                return new
                {
                    donDatSanId = d.DonDatSanID,
                    sanId = d.SanID,
                    maHoaDon = d.DonDatSanID.ToString("D3"),
                    // BỔ SUNG CÁC TRƯỜNG THÔNG TIN MỚI CHO HÓA ĐƠN
                    khachHang = d.NguoiDung != null ? d.NguoiDung.HoTen : "Khách hàng",
                    soDienThoai = d.NguoiDung != null ? d.NguoiDung.SDT : "",
                    tenSan = d.SanPickleball != null ? d.SanPickleball.TenSan : "",
                    loaiSan = d.SanPickleball != null ? d.SanPickleball.LoaiSan : string.Empty,
                    ngayChoiDisplay = d.NgayChoi.HasValue ? d.NgayChoi.Value.ToString("dd/MM/yyyy") : "",
                    khungGio = FormatKhungGioHienThi(d.KhungGio),
                    ngayThanhToan = d.ThanhToans != null && d.ThanhToans.Any()
                        ? d.ThanhToans.First().NgayThanhToan?.ToString("dd/MM/yyyy HH:mm")
                        : (d.NgayTao.HasValue ? d.NgayTao.Value.ToString("dd/MM/yyyy HH:mm") : ""),
                    phuongThucThanhToan = d.ThanhToans != null && d.ThanhToans.Any() ? d.ThanhToans.First().PhuongThuc : "Chuyển khoản",
                    // CÁC TRƯỜNG CŨ KẾT HỢP
                    tongTien = d.TongTien,
                    soTienGiam = d.SoTienGiam ?? 0,
                    trangThai = d.TrangThaiDon,
                    yeuCauHuy = d.YeuCauHuy,
                    yeuCauSua = d.YeuCauSua,
                    daDanhGia = review != null,
                    soSao = review != null ? review.SoSao : 5,
                    binhLuan = review != null ? review.BinhLuan : "",
                    ngayChoi = d.NgayChoi.HasValue ? d.NgayChoi.Value.ToString("yyyy-MM-dd") : "",
                    khungGioGoc = d.KhungGio
                };
            }).ToList();

            return Json(result);
        }
        [HttpPost]
        public async Task<IActionResult> RequestEditBooking([FromBody] EditBookingRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var don = await _context.DonDatSan.Include(d => d.ThanhToans).FirstOrDefaultAsync(d => d.DonDatSanID == request.BookingID && d.NguoiDungID == userId);
            if (don == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

            // Tính toán bù trừ
            decimal daThanhToan = don.ThanhToans?.Sum(t => t.SoTien) ?? 0;
            decimal tongTienMoi = request.TongTien;

            string msg = "";
            if (tongTienMoi > daThanhToan)
            {
                msg = $"Hệ thống ghi nhận bạn cần thanh toán thêm {(tongTienMoi - daThanhToan):N0}đ khi check-in.";
            }
            else if (tongTienMoi < daThanhToan)
            {
                msg = $"Hệ thống ghi nhận bạn sẽ được hoàn lại {(daThanhToan - tongTienMoi):N0}đ khi check-in.";
            }
            else
            {
                msg = "Không phát sinh chênh lệch chi phí.";
            }

            don.YeuCauSua = true;
            var tenSan = await _context.SanPickleball.Where(s => s.SanID == request.SanID).Select(s => s.TenSan).FirstOrDefaultAsync();
            don.NoiDungSua = $"Đổi thành: {tenSan}, Ngày {request.NgayDat:dd/MM/yyyy}, Giờ: {string.Join(", ", request.SelectedHours)}. {msg}";

            _context.DonDatSan.Update(don);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã gửi yêu cầu sửa đến quản lý! {msg}" });
        }

        // Model nhận dữ liệu
        
        [HttpPost]
        public async Task<IActionResult> ToggleCancelRequest([FromBody] CancelRequestModel request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            var don = await _context.DonDatSan.FirstOrDefaultAsync(d => d.DonDatSanID == request.DonDatSanID && d.NguoiDungID == userId);
            if (don == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

            don.YeuCauHuy = request.IsRequesting;
            _context.DonDatSan.Update(don);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class CancelRequestModel
        {
            public int DonDatSanID { get; set; }
            public bool IsRequesting { get; set; } // true: Gửi yêu cầu, false: Thu hồi
        }
        [HttpPost]
        public async Task<IActionResult> SubmitReview([FromBody] ReviewRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập để đánh giá!" });

            var existingReview = await _context.DanhGia.FirstOrDefaultAsync(r => r.DonDatSanID == request.DonDatSanID);
            if (existingReview != null) return Json(new { success = false, message = "Đơn này đã được đánh giá rồi!" });

            var review = new DanhGia
            {
                NguoiDungID = userId.Value,
                SanID = request.SanID,
                DonDatSanID = request.DonDatSanID,
                SoSao = request.SoSao,
                BinhLuan = request.BinhLuan,
                NgayDanhGia = DateTime.Now
            };

            _context.DanhGia.Add(review);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class ReviewRequest
        {
            public int SanID { get; set; }
            public int DonDatSanID { get; set; }
            public int SoSao { get; set; }
            public string? BinhLuan { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetCourtReviews(int sanId)
        {
            // Lấy toàn bộ đánh giá của sân này, bao gồm cả thông tin người dùng
            var reviews = await _context.DanhGia
                .Include(r => r.NguoiDung)
                .Where(r => r.SanID == sanId)
                .OrderByDescending(r => r.NgayDanhGia)
                .Select(r => new {
                    hoTen = r.NguoiDung != null && !string.IsNullOrEmpty(r.NguoiDung.HoTen) ? r.NguoiDung.HoTen : "Khách hàng ẩn danh",
                    soSao = r.SoSao ?? 5,
                    binhLuan = r.BinhLuan,
                    ngayDanhGia = r.NgayDanhGia.HasValue ? r.NgayDanhGia.Value.ToString("yyyy-MM-dd HH:mm") : ""
                })
                .ToListAsync();

            // Tính toán các thông số thống kê
            var totalReviews = reviews.Count;
            var avgRating = totalReviews > 0 ? Math.Round(reviews.Average(r => r.soSao), 1) : 0;

            var starCounts = new
            {
                s5 = reviews.Count(r => r.soSao == 5),
                s4 = reviews.Count(r => r.soSao == 4),
                s3 = reviews.Count(r => r.soSao == 3),
                s2 = reviews.Count(r => r.soSao == 2),
                s1 = reviews.Count(r => r.soSao == 1)
            };

            return Json(new { success = true, data = reviews, stats = new { total = totalReviews, avg = avgRating, counts = starCounts } });
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
        public string? PaymentType { get; set; } // THÊM DÒNG NÀY (VD: "Coc50" hoặc "Full")
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
    public class EditBookingRequest
    {
        public int BookingID { get; set; }
        public int SanID { get; set; }
        public DateTime NgayDat { get; set; }
        public List<int> SelectedHours { get; set; } = new();
        public decimal TongTien { get; set; }
    }
}
