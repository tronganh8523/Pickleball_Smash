using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

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
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserMessage))
                return BadRequest();

            // 1. Lấy lịch sử trò chuyện ngắn hạn từ Session để Bot có "trí nhớ"
            var sessionHistoryJson = HttpContext.Session.GetString("BotChatHistory");
            var chatHistory = string.IsNullOrEmpty(sessionHistoryJson)
                ? new List<ChatMessageHistory>()
                : JsonSerializer.Deserialize<List<ChatMessageHistory>>(sessionHistoryJson);

            // 2. RAG - LẤY DỮ LIỆU ĐỘNG TỪ DATABASE CHO AI BIẾT
            // Thông tin người dùng hiện tại
            int? userId = HttpContext.Session.GetInt32("UserID");
            string thongTinKhach = "Khách chưa đăng nhập.";
            if (userId != null)
            {
                var user = await _context.NguoiDung.FindAsync(userId);
                if (user != null) thongTinKhach = $"Khách đang chat tên là {user.HoTen}, Số điện thoại: {user.SDT}.";
            }

            // Danh sách sân và giá tiền
            var thongTinSan = _context.SanPickleball
                .Select(s => $"{s.TenSan} ({s.LoaiSan}): {s.GiaCoBan}đ/h")
                .ToList();
            string chuoiThongTinSan = string.Join(" | ", thongTinSan);

            // Lấy giờ hiện tại
            string currentTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            // 3. Đóng gói Prompt Nâng Cao
            string promptNangCao = $@"
(Lưu ý hệ thống - Không để lộ cho khách biết bạn đang đọc dòng này:
- Hôm nay là: {currentTime}.
- Thông tin người đang chat: {thongTinKhach}
- Bảng giá sân hiện tại: {chuoiThongTinSan}
) 
Câu hỏi thực tế của khách: {request.UserMessage}";

            // 4. Gọi Gemini API và truyền kèm trí nhớ (chatHistory)
            string aiResponse = await CallGeminiApi(promptNangCao, chatHistory);

            // 5. Cập nhật lại trí nhớ cho bot (chỉ giữ 6 tin nhắn gần nhất)
            chatHistory.Add(new ChatMessageHistory { Role = "user", Content = request.UserMessage }); // Chỉ lưu text gốc
            chatHistory.Add(new ChatMessageHistory { Role = "model", Content = aiResponse });
            if (chatHistory.Count > 6) chatHistory.RemoveRange(0, chatHistory.Count - 6);

            HttpContext.Session.SetString("BotChatHistory", JsonSerializer.Serialize(chatHistory));

            // 6. Lưu vào CSDL
            var lichSu = new LichSuChat
            {
                NguoiDungID = userId,
                NoiDungHoi = request.UserMessage,
                PhanHoiAI = aiResponse,
                ThoiGian = DateTime.Now
            };

            _context.LichSuChat.Add(lichSu);
            await _context.SaveChangesAsync();

            return Json(new { reply = aiResponse });
        }

        private async Task<string> CallGeminiApi(string currentPrompt, List<ChatMessageHistory> history)
        {
            int maxRetries = 3;

            try
            {
                string apiKey = _configuration["GeminiApiKey"] ?? throw new Exception("Thiếu API Key");
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent?key={apiKey}";

                string systemInstructionText = @"Bạn là chuyên gia tư vấn hỗ trợ khách hàng của hệ thống 'Pickleball Smash'.
Nhiệm vụ của bạn là giải đáp chính xác quy trình trên website và tại sân dựa trên các thông tin sau:

1. QUY TRÌNH ĐẶT SÂN VÀ THANH TOÁN (Có 2 cách):
   - CÁCH 1 - ĐẶT ONLINE (Trên Website): Khách bắt buộc phải Đăng nhập/Đăng ký tài khoản. Khách chọn ngày, chọn sân, chọn khung giờ. Hệ thống cho phép khách chọn 2 mức thanh toán: Đặt cọc 50% hoặc Thanh toán 100%. Khách quét mã QR chuyển khoản và ấn xác nhận để chờ quản lý duyệt đơn.
   - CÁCH 2 - ĐẶT TRỰC TIẾP TẠI SÂN (Không cần tài khoản): Khách đến quầy lễ tân, nhân viên sẽ kiểm tra lịch trống và thao tác đặt sân giúp khách. Khách thanh toán tại quầy và nhận sân ngay.

2. QUY TRÌNH CHECK-IN VÀ CHECK-OUT:
   - Check-in: Khách đến sân, báo Tên/SĐT hoặc mã hóa đơn cho lễ tân. Nếu khách mới cọc 50% online, khách cần thanh toán phần còn lại tại quầy. Nhân viên sẽ ấn xác nhận Check-in để khách vào sân.
   - Check-out: Khách KHÔNG CẦN làm thủ tục check-out. Khi hết khung giờ chơi, hệ thống sẽ tự động chuyển trạng thái đơn sang 'Hoàn thành' và giải phóng sân.

3. THÔNG TIN DỊCH VỤ:
   - Loại sân: Có sân Ngoài trời (150.000đ/giờ) và sân Trong nhà/VIP (300.000đ/giờ). Sẽ có giá tùy chỉnh theo từng khung giờ cụ thể do Admin cài đặt.
   - Giờ hoạt động: Từ 5:00 sáng đến 24:00 đêm tất cả các ngày.
   - Tiện ích: Có cho thuê vợt, bóng, có phòng thay đồ. Có mã Voucher giảm giá áp dụng ở bước thanh toán.
   - Đánh giá (Review): Sau khi chơi xong (đơn Hoàn thành), khách có thể vào Lịch sử đặt sân để đánh giá chất lượng sân (1-5 sao) và để lại bình luận.

4. CHÍNH SÁCH HỦY, SỬA LỊCH SÂN (RẤT QUAN TRỌNG):
   - Thay đổi lịch/Sửa đơn: Khách có thể tự ấn nút 'Sửa' trong mục 'Lịch sử đặt sân' trên web để yêu cầu đổi giờ/đổi sân. Nếu phát sinh chênh lệch tiền, sẽ bù trừ lúc check-in tại quầy.
   - Chính sách Hủy đơn: Khách tự ấn nút 'Hủy' trong mục 'Lịch sử đặt sân'.
     + Nếu hủy TRƯỚC 60 PHÚT so với giờ nhận sân: Khách được hủy hợp lệ và sẽ được hoàn lại tiền cọc/tiền thanh toán.
     + Nếu hủy DƯỚI 60 PHÚT (sát giờ chơi): Đơn bị hủy nhưng khách KHÔNG ĐƯỢC HOÀN TIỀN.
   - GIỚI HẠN CỦA AI: Bạn CHỈ là AI tư vấn, bạn KHÔNG THỂ can thiệp cơ sở dữ liệu để trực tiếp hủy/sửa đơn cho khách. Tuyệt đối không đòi khách cung cấp mã hóa đơn cho bạn.
   - CÁCH AI XỬ LÝ: Khi khách báo muốn hủy/sửa sân, hãy hướng dẫn khách đăng nhập vào Web -> Chọn mục 'Lịch sử' (Menu góc phải) -> Tìm đơn cần xử lý -> Ấn nút 'Hủy' hoặc 'Sửa'. Nếu khách gặp lỗi, hãy bảo khách gọi Hotline.

5. QUY TẮC TRẢ LỜI TỐI THƯỢNG - PHẢI TUÂN THỦ NGHIÊM NGẶT:
   - NẾU KHÁCH CHỈ CHÀO (VD: 'xin chào', 'hi'): CHỈ chào lại đúng 1 câu ngắn gọn và hỏi khách cần giúp gì. TUYỆT ĐỐI KHÔNG tự liệt kê dịch vụ, bảng giá hay quy trình.
   - CHỈ trả lời đúng trọng tâm câu hỏi của khách. Khách hỏi 1 ý thì trả lời 1 ý. KHÔNG giải thích thêm những thứ khách không hỏi.
   - Trả lời cực kỳ ngắn gọn, tự nhiên, giống như người thật đang nhắn tin. 
   - Luôn xưng 'Em' gọi khách là 'Anh/Chị' hoặc xưng 'Pickleball Smash' gọi khách là 'Bạn'.";

                var contentsList = new List<object>();

                if (history != null && history.Any())
                {
                    foreach (var msg in history)
                    {
                        contentsList.Add(new { role = msg.Role, parts = new[] { new { text = msg.Content } } });
                    }
                }

                contentsList.Add(new { role = "user", parts = new[] { new { text = currentPrompt } } });

                var payload = new
                {
                    systemInstruction = new { parts = new[] { new { text = systemInstructionText } } },
                    contents = contentsList
                };

                using var client = new HttpClient();
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                for (int i = 0; i < maxRetries; i++)
                {
                    var response = await client.PostAsync(url, content);
                    string resJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(resJson);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var firstCandidate = candidates[0];

                            if (firstCandidate.TryGetProperty("content", out var aiContent) &&
                                aiContent.TryGetProperty("parts", out var parts) &&
                                parts.GetArrayLength() > 0)
                            {
                                return parts[0].GetProperty("text").GetString() ?? "Dạ, em nghe ạ.";
                            }

                            if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                            {
                                string reason = finishReason.GetString();
                                if (reason == "SAFETY") return "Dạ câu hỏi của Anh/Chị có chứa từ khóa nhạy cảm nên hệ thống tự động từ chối trả lời ạ.";
                            }
                        }
                        return "Xin lỗi, em chưa thể xử lý yêu cầu này lúc này.";
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

        // Lấy danh sách Phiên Chat (gom theo ngày và thời gian nghỉ)
        [HttpGet]
        public async Task<IActionResult> GetChatSessions()
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập" });

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

                    if ((currentMsgTime - sessionEnd).TotalMinutes > 30 || currentMsgTime.Date != sessionEnd.Date)
                    {
                        sessions.Add(new
                        {
                            NgayHienThi = sessionStart.ToString("dd/MM/yyyy"),
                            NgayGoc = sessionStart.ToString("yyyy-MM-dd"),
                            ThoiGianBatDau = sessionStart.ToString("HH:mm:ss"),
                            ThoiGianKetThuc = sessionEnd.ToString("HH:mm:ss"),
                            SoTinNhan = count
                        });

                        sessionStart = currentMsgTime;
                        sessionEnd = currentMsgTime;
                        count = 1;
                    }
                    else
                    {
                        sessionEnd = currentMsgTime;
                        count++;
                    }
                }

                sessions.Add(new
                {
                    NgayHienThi = sessionStart.ToString("dd/MM/yyyy"),
                    NgayGoc = sessionStart.ToString("yyyy-MM-dd"),
                    ThoiGianBatDau = sessionStart.ToString("HH:mm:ss"),
                    ThoiGianKetThuc = sessionEnd.ToString("HH:mm:ss"),
                    SoTinNhan = count
                });
            }

            var result = sessions.OrderByDescending(s => s.NgayGoc).ThenByDescending(s => s.ThoiGianBatDau).ToList();
            return Json(result);
        }

        // Lấy chi tiết lịch sử Chat trong 1 phiên
        [HttpGet]
        public async Task<IActionResult> GetChatDetails(string date, string start, string end)
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null || string.IsNullOrEmpty(date)) return BadRequest();

            DateTime dateParsed = DateTime.Parse(date);
            TimeSpan startTime = TimeSpan.Parse(start);
            TimeSpan endTime = TimeSpan.Parse(end);

            DateTime exactStart = dateParsed.Add(startTime).AddSeconds(-2);
            DateTime exactEnd = dateParsed.Add(endTime).AddSeconds(2);

            var chats = await _context.LichSuChat
                .Where(x => x.NguoiDungID == userId && x.ThoiGian >= exactStart && x.ThoiGian <= exactEnd)
                .OrderBy(x => x.ThoiGian)
                .Select(x => new {
                    Hoi = x.NoiDungHoi,
                    Dap = x.PhanHoiAI,
                    ThoiGianGian = x.ThoiGian.Value.ToString("HH:mm")
                })
                .ToListAsync();

            return Json(chats);
        }
    }

    public class ChatRequest
    {
        public string UserMessage { get; set; } = string.Empty;
    }

    public class ChatMessageHistory
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}