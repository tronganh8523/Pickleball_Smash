using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Pickleball_Smash.Controllers
{
    public class AdminDonDatSanController : Controller
    {
        private readonly AppDbContext _context;

        public AdminDonDatSanController(AppDbContext context)
        {
            _context = context;
        }

        // GET: DonDatSan - View only
        public async Task<IActionResult> Index()
        {
            var items = await _context.DonDatSan
                .Include(d => d.NguoiDung)
                .Include(d => d.SanPickleball)
                .Include(d => d.Voucher)
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            return View("~/Views/Admin/DonDatSan/Index.cshtml", items);
        }

        [HttpGet]
        public async Task<IActionResult> InvoicePdf(int id, bool download = true)
        {
            var don = await _context.DonDatSan
                .Include(d => d.NguoiDung)
                .Include(d => d.SanPickleball)
                .Include(d => d.Voucher)
                .FirstOrDefaultAsync(d => d.DonDatSanID == id);

            if (don == null) return NotFound();

            QuestPDF.Settings.License = LicenseType.Community;

            var culture = CultureInfo.GetCultureInfo("vi-VN");
            string Money(decimal? v) => (v ?? 0m).ToString("N0", culture) + " đ";

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Pickleball Smash").SemiBold().FontSize(16);
                            col.Item().Text("HOÁ ĐƠN ĐẶT SÂN").Bold().FontSize(14).FontColor(Colors.Green.Darken2);
                        });

                        row.ConstantItem(180).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Mã đơn: #{don.DonDatSanID}").SemiBold();
                            col.Item().Text($"Ngày tạo: {don.NgayTao:dd/MM/yyyy HH:mm}");
                        });
                    });

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Element(e =>
                        {
                            e.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                            {
                                c.Spacing(6);
                                c.Item().Text("Thông tin khách hàng").SemiBold();
                                c.Item().Text($"Khách hàng: {don.NguoiDung?.HoTen ?? don.NguoiDung?.TenDangNhap ?? "Khách"}");
                            });
                        });

                        col.Item().Element(e =>
                        {
                            e.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                            {
                                c.Spacing(6);
                                c.Item().Text("Thông tin đặt sân").SemiBold();
                                c.Item().Text($"Sân: {don.SanPickleball?.TenSan ?? "-"}");
                                c.Item().Text($"Ngày chơi: {don.NgayChoi:dd/MM/yyyy}");
                                c.Item().Text($"Khung giờ: {ManagerDonDatSanController.FormatBookingTimeRange(don)}");
                                c.Item().Text($"Trạng thái: {don.TrangThaiDon ?? "-"}");
                            });
                        });

                        col.Item().Element(e =>
                        {
                            e.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                            {
                                c.Spacing(6);
                                c.Item().Text("Thanh toán").SemiBold();
                                c.Item().Text($"Tổng tiền: {Money(don.TongTien)}");
                                c.Item().Text($"Giảm: {Money(don.SoTienGiam)}");
                                c.Item().Text($"Voucher: {don.Voucher?.MaVoucher ?? "-"}");
                                c.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                                c.Item().Text($"Thành tiền: {Money(don.TongTien)}").Bold();
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Cảm ơn bạn đã sử dụng dịch vụ. ");
                        t.Span("Trang ").SemiBold();
                        t.CurrentPageNumber();
                        t.Span("/");
                        t.TotalPages();
                    });
                });
            }).GeneratePdf();

            var fileName = $"hoa-don-#{don.DonDatSanID}.pdf";
            if (!download)
            {
                Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
                return File(pdf, "application/pdf");
            }

            return File(pdf, "application/pdf", fileName);
        }
    }
}
