using Microsoft.AspNetCore.Mvc;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System;

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

            // 1. Gọi thẳng API Google
            string aiResponse = await CallGeminiApi(request.UserMessage);

            // 2. Lưu vào bảng LichSuChat
            int? userId = HttpContext.Session.GetInt32("UserID");
            var lichSu = new LichSuChat
            {
                NguoiDungID = userId,
                NoiDungHoi = request.UserMessage,
                PhanHoiAI = aiResponse,
                ThoiGian = DateTime.Now
            };

            _context.LichSuChat.Add(lichSu);
            await _context.SaveChangesAsync();

            // 3. Trả kết quả về giao diện
            return Json(new { reply = aiResponse });
        }

        private async Task<string> CallGeminiApi(string userMessage)
        {
            // Cấu hình số lần tự động thử lại nếu Google bị quá tải
            int maxRetries = 3;

            try
            {
                string apiKey = _configuration["GeminiApiKey"] ?? throw new Exception("Thiếu API Key");
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                string systemInstructionText = @"Bạn là trợ lý ảo hỗ trợ chăm sóc khách hàng của hệ thống sân Pickleball Smash.
                    Dưới đây là thông tin quan trọng bạn cần biết để trả lời khách:
                    - Hệ thống có sân ngoài trời (150.000đ/giờ) và sân trong nhà (300.000đ/giờ).
                    - Giờ mở cửa: 5:00 sáng đến 22:00 tối hàng ngày.
                    - Khách có thể đặt sân online qua website hoặc tới trực tiếp quầy.
                    - Chúng tôi có cho thuê vợt và bóng.
                    Hãy trả lời ngắn gọn, lịch sự, thân thiện và xưng hô là 'mình/em' và gọi khách là 'bạn/anh/chị'. 
                    Chỉ trả lời những câu hỏi liên quan đến việc đặt sân, thể thao, hoặc Pickleball.";

                var payload = new
                {
                    systemInstruction = new { parts = new[] { new { text = systemInstructionText } } },
                    contents = new[] { new { parts = new[] { new { text = userMessage } } } }
                };

                using var client = new HttpClient();
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // Vòng lặp Auto-Retry
                for (int i = 0; i < maxRetries; i++)
                {
                    var response = await client.PostAsync(url, content);
                    string resJson = await response.Content.ReadAsStringAsync();

                    // Nếu thành công, xử lý và trả về luôn
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

                    // Nếu Google báo lỗi 503 (Quá tải) và vẫn còn lượt thử
                    if ((int)response.StatusCode == 503 && i < maxRetries - 1)
                    {
                        // Cho hệ thống nghỉ ngơi 2 giây rồi vòng lại hỏi tiếp
                        await Task.Delay(2000);
                        continue;
                    }

                    // Nếu là lỗi khác (ví dụ sai API key, hết tiền...) thì ghi log ngầm để dev biết
                    Console.WriteLine($"Lỗi từ Google API: {resJson}");
                    break; // Thoát vòng lặp
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi hệ thống C#: {ex.Message}");
            }

            // Nếu chạy hết số lần thử mà Google vẫn sập, trả về câu thông báo thân thiện
            return "Xin lỗi bạn, hiện tại đường dây AI đang có quá nhiều người truy cập. Bạn vui lòng thử lại sau ít phút nhé!";
        }

        public class ChatRequest
        {
            public string UserMessage { get; set; } = string.Empty;
        }
    }
}