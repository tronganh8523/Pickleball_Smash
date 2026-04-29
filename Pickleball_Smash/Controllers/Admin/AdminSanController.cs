using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Filters;
using Pickleball_Smash.Models;
using System.Text.Json;

namespace Pickleball_Smash.Controllers
{
    [AdminAuthorize]
    public class AdminSanController : Controller
    {
        private readonly AppDbContext _context;

        public AdminSanController(AppDbContext context)
        {
            _context = context;
        }

        // GET: San - List
        public async Task<IActionResult> Index()
        {
            var items = await _context.SanPickleball.ToListAsync();
            foreach (var item in items)
            {
                item.TrangThai = NormalizeTrangThai(item.TrangThai);
            }
            return View("~/Views/Admin/San/Index.cshtml", items);
        }

        // GET: San - Create
        public IActionResult Create()
        {
            TempData["Error"] = "Vui lòng thao tác tạo sân bằng popup tại trang danh sách.";
            return RedirectToAction(nameof(Index), "AdminSan");
        }

        // POST: San - Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenSan,LoaiSan,MoTa,GiaCoBan,TrangThai")] SanPickleball san)
        {
            san.TrangThai = NormalizeTrangThai(san.TrangThai);

            // duplicate name
            if (!string.IsNullOrWhiteSpace(san.TenSan))
            {
                var tenSan = san.TenSan.Trim().ToLower();
                var exist = await _context.SanPickleball
                    .FirstOrDefaultAsync(x => x.TenSan != null
                        && x.TenSan.ToLower() == tenSan);
                if (exist != null)
                {
                    ModelState.AddModelError("TenSan", "Tên sân đã tồn tại.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(san);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm sân thành công!";
                return RedirectToAction(nameof(Index), "AdminSan");
            }

            SetModalState("create-san", san);
            return RedirectToAction(nameof(Index), "AdminSan");
        }

        // GET: San - Edit
        public IActionResult Edit(int? id)
        {
            TempData["Error"] = "Vui lòng thao tác chỉnh sửa bằng popup tại trang danh sách.";
            return RedirectToAction(nameof(Index), "AdminSan");
        }

        // POST: San - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SanID,TenSan,LoaiSan,MoTa,GiaCoBan,TrangThai")] SanPickleball san)
        {
            if (id != san.SanID) return NotFound();
            san.TrangThai = NormalizeTrangThai(san.TrangThai);
            if (!string.IsNullOrWhiteSpace(san.TenSan))
            {
                var tenSan = san.TenSan.Trim().ToLower();
                var exist = await _context.SanPickleball
                    .FirstOrDefaultAsync(x => x.TenSan != null
                        && x.TenSan.ToLower() == tenSan
                        && x.SanID != san.SanID);
                if (exist != null)
                {
                    ModelState.AddModelError("TenSan", "Tên sân đã tồn tại.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(san);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật sân thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.SanPickleball.Any(e=>e.SanID==san.SanID))
                        return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index), "AdminSan");
            }

            SetModalState("edit-san", san);
            return RedirectToAction(nameof(Index), "AdminSan");
        }

        // GET: San - Delete
        public IActionResult Delete(int? id)
        {
            TempData["Error"] = "Chức năng xóa trực tiếp bằng popup, không dùng trang Delete riêng.";
            return RedirectToAction(nameof(Index), "AdminSan");
        }

        // POST: San - Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var san = await _context.SanPickleball.FindAsync(id);
            if(san!=null)
            {
                _context.SanPickleball.Remove(san);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa sân thành công!";
            }
            return RedirectToAction(nameof(Index), "AdminSan");
        }

        private static string NormalizeTrangThai(string? trangThai)
        {
            if (string.IsNullOrWhiteSpace(trangThai))
            {
                return "Trống";
            }

            if (trangThai.Equals("Mở", StringComparison.OrdinalIgnoreCase)
                || trangThai.Equals("Trong", StringComparison.OrdinalIgnoreCase)
                || trangThai.Equals("Trống", StringComparison.OrdinalIgnoreCase))
            {
                return "Trống";
            }

            if (trangThai.Equals("Đóng", StringComparison.OrdinalIgnoreCase)
                || trangThai.Equals("Ban", StringComparison.OrdinalIgnoreCase)
                || trangThai.Equals("Bận", StringComparison.OrdinalIgnoreCase))
            {
                return "Bận";
            }

            return "Trống";
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

