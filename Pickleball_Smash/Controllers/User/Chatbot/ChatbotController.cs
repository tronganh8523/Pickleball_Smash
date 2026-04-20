using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Pickleball_Smash.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public ChatbotController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserMessage))
                return BadRequest();

            // 1. Lấy danh sách tên sân hiện có từ DB để 'nhắc' cho AI biết
            var danhSachSan = string.Join(", ", _context.SanPickleball.Select(s => s.TenSan).ToList());

            // 2. Gắn thêm thông tin này vào trước tin nhắn của người dùng (ẩn với giao diện)
            string promptNangCao = $" (Lưu ý: Hiện tại hệ thống đang có các sân: {danhSachSan}). Câu hỏi của khách: {request.UserMessage}";

            // 3. Gọi Gemini API (Chỉ khai báo biến aiResponse 1 lần duy nhất ở đây)
            string aiResponse = await CallGeminiApi(promptNangCao);

            // 4. Lưu vào bảng LichSuChat
            int? userId = HttpContext.Session.GetInt32("UserID");
            var lichSu = new LichSuChat
            {
                NguoiDungID = userId,
                // Chú ý: Chỉ lưu câu hỏi gốc của khách vào CSDL cho đẹp, không lưu promptNangCao
                NoiDungHoi = request.UserMessage,
                PhanHoiAI = aiResponse,
                ThoiGian = DateTime.Now
            };

            _context.LichSuChat.Add(lichSu);
            await _context.SaveChangesAsync();

            // 5. Trả kết quả về giao diện
            return Json(new { reply = aiResponse });
        }

        private async Task<string> CallGeminiApi(string userMessage)
        {
            int maxRetries = 3;

            try
            {
                string apiKey = _configuration["GeminiApiKey"] ?? throw new Exception("Thiếu API Key");
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent?key={apiKey}";

                // 1. Chỉ dẫn hệ thống (Tách riêng để AI hiểu rõ vai trò)
                // 1. Chỉ dẫn hệ thống (Tách riêng để AI hiểu rõ vai trò)
                string systemInstructionText = @"Bạn là chuyên gia tư vấn hỗ trợ khách hàng của hệ thống 'Pickleball Smash'.
Nhiệm vụ của bạn là giải đáp chính xác quy trình trên website và tại sân dựa trên các thông tin sau:

1. QUY TRÌNH ĐẶT SÂN (Có 2 cách):
   - CÁCH 1 - ĐẶT ONLINE (Trên Website): Khách bắt buộc phải Đăng nhập/Đăng ký tài khoản. Sau đó chọn sân, chọn giờ, hệ thống tính tiền và khách quét mã QR để chuyển khoản.
   - CÁCH 2 - ĐẶT TRỰC TIẾP TẠI SÂN (Không cần tài khoản): Khách hàng KHÔNG cần tạo tài khoản trên website. Khách chỉ cần đến quầy lễ tân, nhân viên sẽ kiểm tra lịch trống và thao tác đặt sân giúp khách. Khách thanh toán (tiền mặt hoặc chuyển khoản) và check-in nhận sân ngay tại chỗ.

2. QUY TRÌNH TÀI KHOẢN (Dành cho khách muốn đặt Online):
   - Đăng ký bằng: Tên đăng nhập, Email, SĐT và Mật khẩu.
   - Mục 'Lịch sử' trên web cho phép khách xem lại các đơn đặt sân đã thanh toán.

3. THÔNG TIN DỊCH VỤ:
   - Loại sân: Có sân Ngoài trời (150.000đ/giờ) và sân Trong nhà/VIP (300.000đ/giờ).
   - Giờ hoạt động: Từ 5:00 sáng đến 22:00 tối tất cả các ngày.
   - Tiện ích: Có cho thuê vợt, bóng, có phòng thay đồ và nước uống miễn phí (tùy loại sân).
   - Khuyến mãi: Website có chức năng nhập mã Voucher giảm giá.

4. CHÍNH SÁCH HỦY SÂN VÀ THAY ĐỔI LỊCH (QUAN TRỌNG):
   - Quy định: Khách có thể yêu cầu hủy/thay đổi lịch. Khi hủy thành công, đơn cập nhật thành “Đã hủy” và hoàn tiền theo chính sách.
   - GIỚI HẠN CỦA BẠN (AI): Bạn CHỈ là AI tư vấn, bạn KHÔNG có quyền truy cập cơ sở dữ liệu hóa đơn và KHÔNG THỂ trực tiếp hủy đơn cho khách.
   - CÁCH XỬ LÝ: Khi khách báo muốn hủy sân, TUYỆT ĐỐI KHÔNG yêu cầu khách cung cấp mã hóa đơn cho bạn. Thay vào đó, hãy thông báo chính sách hủy và lịch sự hướng dẫn khách gọi điện thoại trực tiếp vào Hotline hoặc nhắn tin qua Fanpage để nhân viên con người hỗ trợ kiểm tra và hủy đơn.

5. QUY TẮC TRẢ LỜI TỐI THƯỢNG - PHẢI TUÂN THỦ NGHIÊM NGẶT
- NẾU KHÁCH CHỈ CHÀO (VD: 'xin chào', 'hi'): CHỈ chào lại đúng 1 câu ngắn gọn và hỏi khách cần giúp gì. TUYỆT ĐỐI KHÔNG tự liệt kê dịch vụ, bảng giá hay quy trình.
- CHỈ trả lời đúng trọng tâm câu hỏi của khách. Khách hỏi 1 ý thì trả lời 1 ý. KHÔNG giải thích thêm những thứ khách không hỏi.
- Trả lời cực kỳ ngắn gọn, tự nhiên, giống như người thật đang nhắn tin. 
- Luôn xưng 'Em' gọi khách là 'Anh/Chị' hoặc xưng 'Pickleball Smash' gọi khách là 'Bạn'.";

                // 2. Nạp thêm danh sách sân thực tế từ Database vào câu hỏi (Đã sửa lỗi biến ở đây)
                var danhSachSan = string.Join(", ", _context.SanPickleball.Select(s => s.TenSan).ToList());
                string promptNangCao = $"(Lưu ý nội bộ: Hiện tại hệ thống đang có các sân: {danhSachSan}). Câu hỏi của khách: {userMessage}";

                // 3. Đóng gói JSON chuẩn cấu trúc
                var payload = new
                {
                    systemInstruction = new { parts = new[] { new { text = systemInstructionText } } },
                    contents = new[] { new { parts = new[] { new { text = promptNangCao } } } }
                };

                using var client = new HttpClient();
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // Vòng lặp Auto-Retry
                for (int i = 0; i < maxRetries; i++)
                {
                    var response = await client.PostAsync(url, content);
                    string resJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(resJson);
                        var aiText = doc.RootElement
                                        .GetProperty("candidates")[0]
                                        .GetProperty("content")
                                        .GetProperty("parts")[0]
                                        .GetProperty("text").GetString();

                        return aiText ?? "Xin lỗi, tôi chưa hiểu ý bạn lắm.";
                    }

                    if ((int)response.StatusCode == 503 && i < maxRetries - 1)
                    {
                        await Task.Delay(2000);
                        continue;
                    }

                    Console.WriteLine($"Lỗi từ Google API: {resJson}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi hệ thống C#: {ex.Message}");
            }

            return "Xin lỗi bạn, hiện tại đường dây AI đang có quá nhiều người truy cập. Bạn vui lòng thử lại sau ít phút nhé!";
        }

        public class ChatRequest
        {
            public string UserMessage { get; set; } = string.Empty;
        }
        // 1. API Lấy danh sách Phiên Chat (Tự động tách phiên dựa trên thời gian)
        [HttpGet]
        public async Task<IActionResult> GetChatSessions()
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập" });

            // Lấy lịch sử, SẮP XẾP TĂNG DẦN để duyệt từ cũ đến mới
            var history = await _context.LichSuChat
                .Where(x => x.NguoiDungID == userId && x.ThoiGian != null)
                .OrderBy(x => x.ThoiGian)
                .ToListAsync();

            var sessions = new List<dynamic>();

            if (history.Any())
            {
                DateTime sessionStart = history[0].ThoiGian.Value;
                DateTime sessionEnd = history[0].ThoiGian.Value;
                int count = 1;

                for (int i = 1; i < history.Count; i++)
                {
                    var currentMsgTime = history[i].ThoiGian.Value;

                    // NẾU khoảng cách giữa 2 tin nhắn lớn hơn 30 phút, HOẶC sang ngày mới -> Cắt thành phiên mới
                    if ((currentMsgTime - sessionEnd).TotalMinutes > 30 || currentMsgTime.Date != sessionEnd.Date)
                    {
                        // Lưu lại phiên cũ
                        sessions.Add(new
                        {
                            NgayHienThi = sessionStart.ToString("dd/MM/yyyy"),
                            NgayGoc = sessionStart.ToString("yyyy-MM-dd"),
                            ThoiGianBatDau = sessionStart.ToString("HH:mm:ss"),
                            ThoiGianKetThuc = sessionEnd.ToString("HH:mm:ss"),
                            SoTinNhan = count
                        });

                        // Khởi tạo phiên mới
                        sessionStart = currentMsgTime;
                        sessionEnd = currentMsgTime;
                        count = 1;
                    }
                    else
                    {
                        // Vẫn trong cùng 1 phiên, cập nhật giờ kết thúc
                        sessionEnd = currentMsgTime;
                        count++;
                    }
                }

                // Lưu lại phiên cuối cùng
                sessions.Add(new
                {
                    NgayHienThi = sessionStart.ToString("dd/MM/yyyy"),
                    NgayGoc = sessionStart.ToString("yyyy-MM-dd"),
                    ThoiGianBatDau = sessionStart.ToString("HH:mm:ss"),
                    ThoiGianKetThuc = sessionEnd.ToString("HH:mm:ss"),
                    SoTinNhan = count
                });
            }

            // Trả về danh sách, sắp xếp phiên mới nhất (vừa chat) lên đầu
            var result = sessions.OrderByDescending(s => s.NgayGoc).ThenByDescending(s => s.ThoiGianBatDau).ToList();
            return Json(result);
        }

        // 2. API Lấy chi tiết đoạn hội thoại của 1 PHIÊN cụ thể
        [HttpGet]
        public async Task<IActionResult> GetChatDetails(string date, string start, string end)
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null || string.IsNullOrEmpty(date)) return BadRequest();

            DateTime dateParsed = DateTime.Parse(date);
            TimeSpan startTime = TimeSpan.Parse(start);
            TimeSpan endTime = TimeSpan.Parse(end);

            // Cộng trừ hao 2 giây để tránh lỗi sai số Mili-giây trong SQL Server
            DateTime exactStart = dateParsed.Add(startTime).AddSeconds(-2);
            DateTime exactEnd = dateParsed.Add(endTime).AddSeconds(2);

            var chats = await _context.LichSuChat
                .Where(x => x.NguoiDungID == userId && x.ThoiGian >= exactStart && x.ThoiGian <= exactEnd)
                .OrderBy(x => x.ThoiGian)
                .Select(x => new
                {
                    Hoi = x.NoiDungHoi,
                    Dap = x.PhanHoiAI,
                    ThoiGianGian = x.ThoiGian.Value.ToString("HH:mm")
                })
                .ToListAsync();

            return Json(chats);
        }
    }
}