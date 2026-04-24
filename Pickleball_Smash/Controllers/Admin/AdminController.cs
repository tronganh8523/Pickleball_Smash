using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using System.Globalization;
using System.IO;
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
                .Where(d => d.TrangThaiDon != null
                    && (d.TrangThaiDon.Trim() == "Hoàn thành" || d.TrangThaiDon.Trim() == "Đã hoàn thành"))
                .CountAsync();
            var doanhThuHoanThanh = await _context.DonDatSan
                .Where(d => d.TrangThaiDon != null
                    && (d.TrangThaiDon.Trim() == "Hoàn thành" || d.TrangThaiDon.Trim() == "Đã hoàn thành"))
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
                    { "type", "bar" },
                    { "data", counts },
                    { "backgroundColor", "rgba(13, 110, 253, 0.65)" },
                    { "borderColor", "#0d6efd" },
                    { "borderWidth", 1 },
                    { "borderRadius", 6 },
                    { "maxBarThickness", 44 },
                    { "yAxisID", "y" }
                },
                new Dictionary<string, object>
                {
                    { "label", "Doanh Thu (VNĐ)" },
                    { "type", "line" },
                    { "data", revenues },
                    { "borderColor", "#198754" },
                    { "backgroundColor", "#198754" },
                    { "tension", 0.4 },
                    { "fill", false },
                    { "borderWidth", 2 },
                    { "pointRadius", 0 },
                    { "pointHoverRadius", 4 },
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
                .Where(d => d.TrangThaiDon != null && new[]
                {
                    "Hoàn thành",
                    "Đã hoàn thành",
                    "Đã huỷ",
                    "Đã hủy"
                }.Contains(d.TrangThaiDon.Trim()))
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Booking Report");

            // Header
            ws.Cell(1, 1).Value = "Mã Đơn";
            ws.Cell(1, 2).Value = "Sân";
            ws.Cell(1, 3).Value = "Khách Hàng";
            ws.Cell(1, 4).Value = "Ngày Chơi";
            ws.Cell(1, 5).Value = "Khung Giờ";
            ws.Cell(1, 6).Value = "Tổng Tiền (VNĐ)";
            ws.Cell(1, 7).Value = "Trạng Thái";
            ws.Cell(1, 8).Value = "Ngày Tạo";

            ws.Range(1, 1, 1, 8).Style.Font.Bold = true;
            ws.Range(1, 1, 1, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4F7");

            // Rows
            var row = 2;
            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = item.DonDatSanID;
                ws.Cell(row, 2).Value = item.SanPickleball?.TenSan ?? "-";
                ws.Cell(row, 3).Value = item.NguoiDung?.HoTen ?? item.NguoiDung?.TenDangNhap ?? "Khách";
                ws.Cell(row, 4).Value = item.NgayChoi?.ToString("dd/MM/yyyy") ?? "-";
                ws.Cell(row, 5).Value = ManagerDonDatSanController.FormatBookingTimeRange(item);
                ws.Cell(row, 6).Value = (double)(item.TongTien ?? 0m);
                ws.Cell(row, 7).Value = item.TrangThaiDon ?? "-";
                ws.Cell(row, 8).Value = item.NgayTao?.ToString("dd/MM/yyyy HH:mm") ?? "-";
                row++;
            }

            // Format money column with thousands separator
            ws.Column(6).Style.NumberFormat.Format = "#,##0";

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var bytes = stream.ToArray();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"booking-report-{DateTime.Now:yyyy-MM-dd}.xlsx"
            );
        }
    }
}