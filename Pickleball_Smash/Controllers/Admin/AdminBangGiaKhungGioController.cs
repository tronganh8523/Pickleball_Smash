using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Filters;
using Pickleball_Smash.Models;
using Pickleball_Smash.Models.ViewModels;
using System.Text.Json;

namespace Pickleball_Smash.Controllers
{
    [AdminAuthorize]
    public class AdminBangGiaKhungGioController : Controller
    {
        private readonly AppDbContext _context;

        public AdminBangGiaKhungGioController(AppDbContext context)
        {
            _context = context;
        }

        // GET: BangGiaKhungGio - List
        public async Task<IActionResult> Index()
        {
            var items = await _context.BangGiaKhungGio
                .Include(x => x.SanPickleball)
                .OrderBy(x => x.SanID)
                .ThenBy(x => x.KhungGio)
                .ToListAsync();

            await LoadSansAsync();

            return View("~/Views/Admin/BangGiaKhungGio/Index.cshtml", items);
        }

        // GET: BangGiaKhungGio - Create
        public IActionResult Create()
        {
            TempData["Error"] = "Vui lòng thao tác tạo bảng giá bằng popup tại trang danh sách.";
            return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
        }

        // POST: BangGiaKhungGio - Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BangGiaKhungGioCreateViewModel form)
        {
            var selectedHours = new List<int>();

            if (form.SanIDs == null || !form.SanIDs.Any())
            {
                ModelState.AddModelError("SanIDs", "Vui lòng chọn ít nhất 1 sân.");
            }

            if (string.IsNullOrWhiteSpace(form.KhungGio))
            {
                ModelState.AddModelError("KhungGio", "Khung giờ là bắt buộc.");
            }
            else if (!TryParseKhungGioHours(form.KhungGio, out selectedHours, out var khungGioError))
            {
                ModelState.AddModelError("KhungGio", khungGioError);
            }

            if (!form.GiaTien.HasValue || form.GiaTien.Value <= 0)
            {
                ModelState.AddModelError("GiaTien", "Giá tiền phải lớn hơn 0.");
            }

            if (ModelState.IsValid)
            {
                var selectedSanIds = (form.SanIDs ?? new List<int>()).Distinct().ToList();

                foreach (var sanId in selectedSanIds)
                {
                    var occupiedHours = await GetOccupiedHoursForSanAsync(sanId);
                    var hasOverlap = selectedHours.Any(occupiedHours.Contains);

                    if (hasOverlap)
                    {
                        var tenSan = await _context.SanPickleball
                            .Where(x => x.SanID == sanId)
                            .Select(x => x.TenSan)
                            .FirstOrDefaultAsync();

                        ModelState.AddModelError("SanIDs", $"Khung giờ bị trùng ở sân: {tenSan ?? sanId.ToString()}.");
                        break;
                    }
                }
            }

            if (ModelState.IsValid)
            {
                var normalizedKhungGio = NormalizeKhungGio(selectedHours);
                var entities = (form.SanIDs ?? new List<int>())
                    .Distinct()
                    .Select(sanId => new BangGiaKhungGio
                    {
                        SanID = sanId,
                        KhungGio = normalizedKhungGio,
                        GiaTien = form.GiaTien
                    })
                    .ToList();

                _context.BangGiaKhungGio.AddRange(entities);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Thêm bảng giá khung giờ thành công cho {entities.Count} sân!";
                return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
            }

            SetModalState("create-banggia", form);
            return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
        }

        // GET: BangGiaKhungGio - Edit
        public IActionResult Edit(int? id)
        {
            TempData["Error"] = "Vui lòng thao tác chỉnh sửa bằng popup tại trang danh sách.";
            return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
        }

        // POST: BangGiaKhungGio - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaGia,SanID,KhungGio,GiaTien")] BangGiaKhungGio bangGia)
        {
            if (id != bangGia.MaGia)
            {
                return NotFound();
            }

            await ValidateBangGiaAsync(bangGia, bangGia.MaGia);

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bangGia);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật bảng giá khung giờ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.BangGiaKhungGio.Any(e => e.MaGia == bangGia.MaGia))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
            }

            SetModalState("edit-banggia", bangGia);
            return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
        }

        // GET: BangGiaKhungGio - occupied hours for selected courts
        [HttpGet]
        public async Task<IActionResult> GetOccupiedHours([FromQuery] List<int> sanIds, [FromQuery] int? excludeMaGia = null)
        {
            var occupiedHours = new HashSet<int>();
            foreach (var sanId in sanIds.Distinct())
            {
                var occupiedBySan = await GetOccupiedHoursForSanAsync(sanId, excludeMaGia);
                occupiedHours.UnionWith(occupiedBySan);
            }

            return Json(new { occupiedHours = occupiedHours.OrderBy(x => x).ToList() });
        }

        // GET: BangGiaKhungGio - Delete
        public IActionResult Delete(int? id)
        {
            TempData["Error"] = "Chức năng xóa trực tiếp bằng popup, không dùng trang Delete riêng.";
            return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
        }

        // POST: BangGiaKhungGio - Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bangGia = await _context.BangGiaKhungGio.FindAsync(id);
            if (bangGia != null)
            {
                _context.BangGiaKhungGio.Remove(bangGia);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa bảng giá khung giờ thành công!";
            }

            return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
        }

        private async Task LoadSansAsync(IEnumerable<int>? selectedSanIds = null)
        {
            var sans = await _context.SanPickleball
                .OrderBy(x => x.TenSan)
                .ToListAsync();
            ViewBag.Sans = new MultiSelectList(sans, "SanID", "TenSan", selectedSanIds);
        }

        private async Task ValidateBangGiaAsync(BangGiaKhungGio bangGia, int? excludeMaGia = null)
        {
            var selectedHours = new List<int>();

            if (!bangGia.SanID.HasValue)
            {
                ModelState.AddModelError("SanID", "Vui lòng chọn sân.");
            }

            if (string.IsNullOrWhiteSpace(bangGia.KhungGio))
            {
                ModelState.AddModelError("KhungGio", "Khung giờ là bắt buộc.");
            }
            else if (!TryParseKhungGioHours(bangGia.KhungGio, out selectedHours, out var khungGioError))
            {
                ModelState.AddModelError("KhungGio", khungGioError);
            }

            if (!bangGia.GiaTien.HasValue || bangGia.GiaTien.Value <= 0)
            {
                ModelState.AddModelError("GiaTien", "Giá tiền phải lớn hơn 0.");
            }

            if (ModelState.IsValid && bangGia.SanID.HasValue)
            {
                var occupiedHours = await GetOccupiedHoursForSanAsync(bangGia.SanID.Value, excludeMaGia);
                var hasOverlap = selectedHours.Any(occupiedHours.Contains);

                if (hasOverlap)
                {
                    ModelState.AddModelError("KhungGio", "Khung giờ bị trùng với bảng giá đã tồn tại của sân này.");
                }

                bangGia.KhungGio = NormalizeKhungGio(selectedHours);
            }
        }

        private async Task<HashSet<int>> GetOccupiedHoursForSanAsync(int sanId, int? excludeMaGia = null)
        {
            var rows = await _context.BangGiaKhungGio
                .Where(x => x.SanID == sanId && (!excludeMaGia.HasValue || x.MaGia != excludeMaGia.Value))
                .Where(x => !string.IsNullOrWhiteSpace(x.KhungGio))
                .Select(x => x.KhungGio)
                .ToListAsync();

            var occupiedHours = new HashSet<int>();
            foreach (var row in rows)
            {
                if (!TryParseKhungGioHours(row, out var hours, out _))
                {
                    continue;
                }

                foreach (var h in hours)
                {
                    occupiedHours.Add(h);
                }
            }

            return occupiedHours;
        }

        private static bool TryParseKhungGioHours(string? raw, out List<int> hours, out string error)
        {
            hours = new List<int>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Khung giờ là bắt buộc.";
                return false;
            }

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                error = "Khung giờ là bắt buộc.";
                return false;
            }

            var parsed = new List<int>();
            foreach (var part in parts)
            {
                if (!int.TryParse(part, out var hour))
                {
                    error = "Khung giờ không hợp lệ. Ví dụ đúng: 5,6,7.";
                    return false;
                }

                if (hour < 5 || hour > 23)
                {
                    error = "Mỗi giờ trong khung giờ phải nằm trong khoảng 5 đến 23.";
                    return false;
                }

                parsed.Add(hour);
            }

            parsed = parsed.Distinct().OrderBy(x => x).ToList();
            if (!parsed.Any())
            {
                error = "Khung giờ là bắt buộc.";
                return false;
            }

            hours = parsed;
            return true;
        }

        private static string NormalizeKhungGio(IEnumerable<int> hours)
        {
            return string.Join(",", hours.Distinct().OrderBy(x => x));
        }

        private void SetModalState(string openModal, object modalData)
        {
            TempData["OpenModal"] = openModal;
            TempData["ModalData"] = JsonSerializer.Serialize(modalData);
            TempData["ModalErrors"] = string.Join("\n", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());
        }
    }
}
