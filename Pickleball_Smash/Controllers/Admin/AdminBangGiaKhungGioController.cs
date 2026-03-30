using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using Pickleball_Smash.Models.ViewModels;

namespace Pickleball_Smash.Controllers
{
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
                .ThenBy(x => x.GioBatDau)
                .ToListAsync();

            return View("~/Views/Admin/BangGiaKhungGio/Index.cshtml", items);
        }

        // GET: BangGiaKhungGio - Create
        public async Task<IActionResult> Create()
        {
            await LoadSansAsync();
            PrepareTimeOptions();
            return View("~/Views/Admin/BangGiaKhungGio/Create.cshtml", new BangGiaKhungGioCreateViewModel());
        }

        // POST: BangGiaKhungGio - Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BangGiaKhungGioCreateViewModel form)
        {
            if (form.SanIDs == null || !form.SanIDs.Any())
            {
                ModelState.AddModelError("SanIDs", "Vui lòng chọn ít nhất 1 sân.");
            }

            if (!form.GioBatDau.HasValue)
            {
                ModelState.AddModelError("GioBatDau", "Giờ bắt đầu là bắt buộc.");
            }

            if (!form.GioKetThuc.HasValue)
            {
                ModelState.AddModelError("GioKetThuc", "Giờ kết thúc là bắt buộc.");
            }

            if (!form.GiaTien.HasValue || form.GiaTien.Value <= 0)
            {
                ModelState.AddModelError("GiaTien", "Giá tiền phải lớn hơn 0.");
            }

            if (form.GioBatDau.HasValue && !IsValidHourValue(form.GioBatDau.Value, false))
            {
                ModelState.AddModelError("GioBatDau", "Giờ bắt đầu phải từ 06:00 đến 23:00 và cách nhau 1 giờ.");
            }

            if (form.GioKetThuc.HasValue && !IsValidHourValue(form.GioKetThuc.Value, true))
            {
                ModelState.AddModelError("GioKetThuc", "Giờ kết thúc phải từ 07:00 đến 24:00 và cách nhau 1 giờ.");
            }

            if (form.GioBatDau.HasValue && form.GioKetThuc.HasValue)
            {
                var gioBatDau = ToHour(form.GioBatDau.Value, false);
                var gioKetThuc = ToHour(form.GioKetThuc.Value, true);
                if (gioBatDau >= gioKetThuc)
                {
                    ModelState.AddModelError("GioKetThuc", "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
                }
            }

            if (ModelState.IsValid)
            {
                var selectedSanIds = (form.SanIDs ?? new List<int>()).Distinct().ToList();
                var gioBatDau = ToHour(form.GioBatDau!.Value, false);
                var gioKetThuc = ToHour(form.GioKetThuc!.Value, true);

                foreach (var sanId in selectedSanIds)
                {
                    var occupiedHours = await GetOccupiedHoursForSanAsync(sanId);
                    var hasOverlap = Enumerable.Range(gioBatDau, gioKetThuc - gioBatDau)
                        .Any(occupiedHours.Contains);

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
                var entities = (form.SanIDs ?? new List<int>())
                    .Distinct()
                    .Select(sanId => new BangGiaKhungGio
                    {
                        SanID = sanId,
                        GioBatDau = form.GioBatDau,
                        GioKetThuc = form.GioKetThuc,
                        GiaTien = form.GiaTien
                    })
                    .ToList();

                _context.BangGiaKhungGio.AddRange(entities);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Thêm bảng giá khung giờ thành công cho {entities.Count} sân!";
                return RedirectToAction(nameof(Index), "AdminBangGiaKhungGio");
            }

            await LoadSansAsync(form.SanIDs);
            PrepareTimeOptions(form.GioBatDau, form.GioKetThuc);
            return View("~/Views/Admin/BangGiaKhungGio/Create.cshtml", form);
        }

        // GET: BangGiaKhungGio - Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bangGia = await _context.BangGiaKhungGio.FindAsync(id);
            if (bangGia == null)
            {
                return NotFound();
            }

            await LoadSansAsync(bangGia.SanID.HasValue ? new[] { bangGia.SanID.Value } : null);
            PrepareTimeOptions(bangGia.GioBatDau, bangGia.GioKetThuc);
            return View("~/Views/Admin/BangGiaKhungGio/Edit.cshtml", bangGia);
        }

        // POST: BangGiaKhungGio - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaGia,SanID,GioBatDau,GioKetThuc,GiaTien")] BangGiaKhungGio bangGia)
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

            await LoadSansAsync(bangGia.SanID.HasValue ? new[] { bangGia.SanID.Value } : null);
            PrepareTimeOptions(bangGia.GioBatDau, bangGia.GioKetThuc);
            return View("~/Views/Admin/BangGiaKhungGio/Edit.cshtml", bangGia);
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
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bangGia = await _context.BangGiaKhungGio
                .Include(x => x.SanPickleball)
                .FirstOrDefaultAsync(x => x.MaGia == id);

            if (bangGia == null)
            {
                return NotFound();
            }

            return View("~/Views/Admin/BangGiaKhungGio/Delete.cshtml", bangGia);
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
            if (!bangGia.SanID.HasValue)
            {
                ModelState.AddModelError("SanID", "Vui lòng chọn sân.");
            }

            if (!bangGia.GioBatDau.HasValue)
            {
                ModelState.AddModelError("GioBatDau", "Giờ bắt đầu là bắt buộc.");
            }

            if (!bangGia.GioKetThuc.HasValue)
            {
                ModelState.AddModelError("GioKetThuc", "Giờ kết thúc là bắt buộc.");
            }

            if (bangGia.GioBatDau.HasValue && !IsValidHourValue(bangGia.GioBatDau.Value, false))
            {
                ModelState.AddModelError("GioBatDau", "Giờ bắt đầu phải từ 06:00 đến 23:00 và cách nhau 1 giờ.");
            }

            if (bangGia.GioKetThuc.HasValue && !IsValidHourValue(bangGia.GioKetThuc.Value, true))
            {
                ModelState.AddModelError("GioKetThuc", "Giờ kết thúc phải từ 07:00 đến 24:00 và cách nhau 1 giờ.");
            }

            if (bangGia.GioBatDau.HasValue && bangGia.GioKetThuc.HasValue)
            {
                var gioBatDau = ToHour(bangGia.GioBatDau.Value, false);
                var gioKetThuc = ToHour(bangGia.GioKetThuc.Value, true);
                if (gioBatDau >= gioKetThuc)
                {
                    ModelState.AddModelError("GioKetThuc", "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
                }
            }

            if (!bangGia.GiaTien.HasValue || bangGia.GiaTien.Value <= 0)
            {
                ModelState.AddModelError("GiaTien", "Giá tiền phải lớn hơn 0.");
            }

            if (ModelState.IsValid && bangGia.SanID.HasValue && bangGia.GioBatDau.HasValue && bangGia.GioKetThuc.HasValue)
            {
                var gioBatDau = ToHour(bangGia.GioBatDau.Value, false);
                var gioKetThuc = ToHour(bangGia.GioKetThuc.Value, true);

                var occupiedHours = await GetOccupiedHoursForSanAsync(bangGia.SanID.Value, excludeMaGia);
                var hasOverlap = Enumerable.Range(gioBatDau, gioKetThuc - gioBatDau)
                    .Any(occupiedHours.Contains);

                if (hasOverlap)
                {
                    ModelState.AddModelError("GioBatDau", "Khung giờ bị trùng với bảng giá đã tồn tại của sân này.");
                }
            }
        }

        private static bool IsValidHourValue(TimeOnly value, bool isEnd)
        {
            if (value.Minute != 0 || value.Second != 0)
            {
                return false;
            }

            var hour = ToHour(value, isEnd);
            return isEnd
                ? hour >= 7 && hour <= 24
                : hour >= 6 && hour <= 23;
        }

        private static int ToHour(TimeOnly value, bool isEnd)
        {
            if (isEnd && value.Hour == 0 && value.Minute == 0 && value.Second == 0)
            {
                return 24;
            }

            return value.Hour;
        }

        private async Task<HashSet<int>> GetOccupiedHoursForSanAsync(int sanId, int? excludeMaGia = null)
        {
            var rows = await _context.BangGiaKhungGio
                .Where(x => x.SanID == sanId && (!excludeMaGia.HasValue || x.MaGia != excludeMaGia.Value))
                .Where(x => x.GioBatDau.HasValue && x.GioKetThuc.HasValue)
                .Select(x => new { x.GioBatDau, x.GioKetThuc })
                .ToListAsync();

            var occupiedHours = new HashSet<int>();
            foreach (var row in rows)
            {
                var startHour = ToHour(row.GioBatDau!.Value, false);
                var endHour = ToHour(row.GioKetThuc!.Value, true);
                if (startHour >= endHour)
                {
                    continue;
                }

                for (var h = startHour; h < endHour; h++)
                {
                    occupiedHours.Add(h);
                }
            }

            return occupiedHours;
        }

        private void PrepareTimeOptions(TimeOnly? selectedStart = null, TimeOnly? selectedEnd = null)
        {
            var startOptions = new List<SelectListItem>();
            for (var h = 6; h <= 23; h++)
            {
                var value = $"{h:00}:00";
                startOptions.Add(new SelectListItem
                {
                    Value = value,
                    Text = value,
                    Selected = selectedStart.HasValue && ToHour(selectedStart.Value, false) == h
                });
            }

            var endOptions = new List<SelectListItem>();
            for (var h = 7; h <= 24; h++)
            {
                var value = h == 24 ? "00:00" : $"{h:00}:00";
                var text = $"{h:00}:00";
                endOptions.Add(new SelectListItem
                {
                    Value = value,
                    Text = text,
                    Selected = selectedEnd.HasValue && ToHour(selectedEnd.Value, true) == h
                });
            }

            ViewBag.StartHourOptions = startOptions;
            ViewBag.EndHourOptions = endOptions;
        }
    }
}