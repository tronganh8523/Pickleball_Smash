using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using System.Text;
using System.Text.Json;

namespace Pickleball_Smash.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var tongSan = await _context.SanPickleball.CountAsync();
            var tongNguoiDung = await _context.NguoiDung.CountAsync();
            var tongDonDat = await _context.DonDatSan.CountAsync();
            var donHoanThanh = await _context.DonDatSan
                .Where(d => d.TrangThaiDon == "Đã hoàn thành")
                .CountAsync();
            var doanhThuHoanThanh = await _context.DonDatSan
                .Where(d => d.TrangThaiDon == "Đã hoàn thành")
                .SumAsync(d => d.TongTien ?? 0);

            ViewBag.TongSan = tongSan;
            ViewBag.TongNguoiDung = tongNguoiDung;
            ViewBag.TongDonDat = tongDonDat;
            ViewBag.DonHoanThanh = donHoanThanh;
            ViewBag.DoanhThuHoanThanh = doanhThuHoanThanh;

            return View();
        }

        // GET: Booking chart data for last 7 days
        [HttpGet]
        public async Task<IActionResult> GetBookingChartData()
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-6);

            var data = await _context.DonDatSan
                .Where(d => d.NgayTao.HasValue && d.NgayTao >= startDate && d.NgayTao <= endDate.AddDays(1))
                .GroupBy(d => d.NgayTao!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count(), Revenue = g.Sum(d => d.TongTien ?? 0) })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var labels = new List<string>();
            var counts = new List<int>();
            var revenues = new List<decimal>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                labels.Add(date.ToString("dd/MM"));
                var item = data.FirstOrDefault(x => x.Date == date);
                counts.Add(item?.Count ?? 0);
                revenues.Add(item?.Revenue ?? 0);
            }

            var datasets = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "label", "Số Đơn Đặt" },
                    { "data", counts },
                    { "borderColor", "#0d6efd" },
                    { "backgroundColor", "rgba(13, 110, 253, 0.1)" },
                    { "tension", 0.4 },
                    { "fill", true },
                    { "pointRadius", 5 },
                    { "pointBackgroundColor", "#0d6efd" },
                    { "pointBorderColor", "#fff" },
                    { "pointBorderWidth", 2 },
                    { "yAxisID", "y" }
                },
                new Dictionary<string, object>
                {
                    { "label", "Doanh Thu (VNĐ)" },
                    { "data", revenues },
                    { "borderColor", "#198754" },
                    { "backgroundColor", "rgba(25, 135, 84, 0.1)" },
                    { "tension", 0.4 },
                    { "fill", false },
                    { "pointRadius", 5 },
                    { "pointBackgroundColor", "#198754" },
                    { "pointBorderColor", "#fff" },
                    { "pointBorderWidth", 2 },
                    { "yAxisID", "y1" }
                }
            };

            return Json(new { labels, datasets });
        }

        // GET: Download booking report
        [HttpGet]
        public async Task<IActionResult> DownloadBookingReport()
        {
            var data = await _context.DonDatSan
                .Include(d => d.SanPickleball)
                .Include(d => d.NguoiDung)
                .Select(d => new
                {
                    d.DonDatSanID,
                    CourtName = d.SanPickleball!.TenSan,
                    CustomerName = d.NguoiDung!.HoTen,
                    d.NgayChoi,
                    d.KhungGio,
                    d.TongTien,
                    d.TrangThaiDon,
                    d.NgayTao
                })
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Mã Đơn,Sân,Khách Hàng,Ngày Chơi,Khung Giờ,Tổng Tiền (VNĐ),Trạng Thái,Ngày Tạo");

            foreach (var item in data)
            {
                csv.AppendLine($"{item.DonDatSanID},{item.CourtName},{item.CustomerName},{item.NgayChoi:dd/MM/yyyy},{item.KhungGio},{item.TongTien:F0},{item.TrangThaiDon},{item.NgayTao:dd/MM/yyyy HH:mm}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"booking-report-{DateTime.Now:yyyy-MM-dd}.csv");
        }
    }
}