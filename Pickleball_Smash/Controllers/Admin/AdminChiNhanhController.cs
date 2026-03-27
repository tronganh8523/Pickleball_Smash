using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class AdminChiNhanhController : Controller
    {
        private readonly AppDbContext _context;

        public AdminChiNhanhController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Chi Nhánh - Danh sách
        public async Task<IActionResult> ChiNhanhIndex()
        {
            var chiNhanhList = await _context.ChiNhanh.ToListAsync();
            return View("~/Views/Admin/ChiNhanh/ChiNhanhIndex.cshtml", chiNhanhList);
        }

        // GET: Chi Nhánh - Thêm mới
        public IActionResult ChiNhanhCreate()
        {
            return View("~/Views/Admin/ChiNhanh/ChiNhanhCreate.cshtml");
        }

        // POST: Chi Nhánh - Thêm mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChiNhanhCreate([Bind("TenChiNhanh,DiaChi,SDT_LienHe")] ChiNhanh chiNhanh)
        {
            var existingName = await _context.ChiNhanh
                .FirstOrDefaultAsync(x => x.TenChiNhanh.ToLower() == chiNhanh.TenChiNhanh.ToLower());

            if (existingName != null)
            {
                ModelState.AddModelError("TenChiNhanh", "Tên chi nhánh này đã tồn tại!");
            }

            if (ModelState.IsValid)
            {
                _context.Add(chiNhanh);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm chi nhánh thành công!";
                return RedirectToAction(nameof(ChiNhanhIndex), "AdminChiNhanh");
            }

            return View("~/Views/Admin/ChiNhanh/ChiNhanhCreate.cshtml", chiNhanh);
        }

        // POST: Chi Nhánh - Sửa qua AJAX
        [HttpPost]
        public async Task<JsonResult> ChiNhanhEditAjax(int id, [FromBody] ChiNhanh chiNhanh)
        {
            if (id != chiNhanh.ChiNhanhID)
            {
                return Json(new { success = false, error = "ID không khớp" });
            }

            var existingName = await _context.ChiNhanh
                .FirstOrDefaultAsync(x => x.TenChiNhanh.ToLower() == chiNhanh.TenChiNhanh.ToLower()
                    && x.ChiNhanhID != id);

            if (existingName != null)
            {
                return Json(new { success = false, error = "Tên chi nhánh này đã tồn tại!" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, errors });
            }

            try
            {
                _context.Update(chiNhanh);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật chi nhánh thành công!" });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ChiNhanhExists(chiNhanh.ChiNhanhID))
                {
                    return Json(new { success = false, error = "Chi nhánh không tồn tại" });
                }

                return Json(new { success = false, error = "Có lỗi khi cập nhật" });
            }
        }

        // POST: Chi Nhánh - Xóa qua AJAX
        [HttpPost]
        public async Task<JsonResult> ChiNhanhDeleteAjax(int id)
        {
            var chiNhanh = await _context.ChiNhanh.FindAsync(id);
            if (chiNhanh == null)
            {
                return Json(new { success = false, error = "Chi nhánh không tồn tại" });
            }

            var sanCount = await _context.SanPickleball
                .Where(s => s.ChiNhanhID == id)
                .CountAsync();

            if (sanCount > 0)
            {
                return Json(new { success = false, error = "Không thể xóa chi nhánh vì có sân pickleball đang sử dụng!" });
            }

            _context.ChiNhanh.Remove(chiNhanh);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xóa chi nhánh thành công!" });
        }

        // GET: Chi Nhánh - Chỉnh sửa
        public async Task<IActionResult> ChiNhanhEdit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiNhanh = await _context.ChiNhanh.FindAsync(id);
            if (chiNhanh == null)
            {
                return NotFound();
            }

            return View("~/Views/Admin/ChiNhanh/ChiNhanhEdit.cshtml", chiNhanh);
        }

        // POST: Chi Nhánh - Chỉnh sửa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChiNhanhEdit(int id, [Bind("ChiNhanhID,TenChiNhanh,DiaChi,SDT_LienHe")] ChiNhanh chiNhanh)
        {
            if (id != chiNhanh.ChiNhanhID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chiNhanh);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật chi nhánh thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChiNhanhExists(chiNhanh.ChiNhanhID))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(ChiNhanhIndex), "AdminChiNhanh");
            }

            return View("~/Views/Admin/ChiNhanh/ChiNhanhEdit.cshtml", chiNhanh);
        }

        // GET: Chi Nhánh - Xác nhận xóa
        public async Task<IActionResult> ChiNhanhDelete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiNhanh = await _context.ChiNhanh
                .FirstOrDefaultAsync(m => m.ChiNhanhID == id);
            if (chiNhanh == null)
            {
                return NotFound();
            }

            return View("~/Views/Admin/ChiNhanh/ChiNhanhDelete.cshtml", chiNhanh);
        }

        // POST: Chi Nhánh - Xóa
        [HttpPost, ActionName("ChiNhanhDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChiNhanhDeleteConfirmed(int id)
        {
            var chiNhanh = await _context.ChiNhanh.FindAsync(id);
            if (chiNhanh != null)
            {
                var sanCount = await _context.SanPickleball
                    .Where(s => s.ChiNhanhID == id)
                    .CountAsync();

                if (sanCount > 0)
                {
                    TempData["Error"] = "Không thể xóa chi nhánh vì có sân pickleball đang sử dụng!";
                    return RedirectToAction(nameof(ChiNhanhIndex), "AdminChiNhanh");
                }

                _context.ChiNhanh.Remove(chiNhanh);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa chi nhánh thành công!";
            }

            return RedirectToAction(nameof(ChiNhanhIndex), "AdminChiNhanh");
        }

        private bool ChiNhanhExists(int id)
        {
            return _context.ChiNhanh.Any(e => e.ChiNhanhID == id);
        }
    }
}
