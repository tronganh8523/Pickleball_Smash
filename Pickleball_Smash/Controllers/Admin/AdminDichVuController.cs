using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class AdminDichVuController : Controller
    {
        private readonly AppDbContext _context;

        public AdminDichVuController(AppDbContext context)
        {
            _context = context;
        }

        // GET: DichVu - List
        public async Task<IActionResult> Index()
        {
            var items = await _context.DichVuPhuTro.ToListAsync();
            return View("~/Views/Admin/DichVu/DichVuIndex.cshtml", items);
        }

        // GET: DichVu - Create
        public IActionResult Create()
        {
            return View("~/Views/Admin/DichVu/DichVuCreate.cshtml");
        }

        // POST: DichVu - Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenDichVu,LoaiDichVu,Gia")] DichVuPhuTro dichVu)
        {
            // Check duplicate by name
            if (!string.IsNullOrWhiteSpace(dichVu.TenDichVu))
            {
                var tenDichVu = dichVu.TenDichVu.Trim().ToLower();
                var exist = await _context.DichVuPhuTro
                    .FirstOrDefaultAsync(x => x.TenDichVu != null
                        && x.TenDichVu.ToLower() == tenDichVu);
                if (exist != null)
                {
                    ModelState.AddModelError("TenDichVu", "Tên dịch vụ đã tồn tại.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(dichVu);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm dịch vụ thành công!";
                return RedirectToAction(nameof(Index), "AdminDichVu");
            }

            return View("~/Views/Admin/DichVu/DichVuCreate.cshtml", dichVu);
        }

        // GET: DichVu - Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var dichVu = await _context.DichVuPhuTro.FindAsync(id);
            if (dichVu == null) return NotFound();
            return View("~/Views/Admin/DichVu/DichVuEdit.cshtml", dichVu);
        }

        // POST: DichVu - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DichVuID,TenDichVu,LoaiDichVu,Gia")] DichVuPhuTro dichVu)
        {
            if (id != dichVu.DichVuID) return NotFound();

            if (!string.IsNullOrWhiteSpace(dichVu.TenDichVu))
            {
                var tenDichVu = dichVu.TenDichVu.Trim().ToLower();
                var exist = await _context.DichVuPhuTro
                    .FirstOrDefaultAsync(x => x.TenDichVu != null
                        && x.TenDichVu.ToLower() == tenDichVu
                        && x.DichVuID != dichVu.DichVuID);
                if (exist != null)
                {
                    ModelState.AddModelError("TenDichVu", "Tên dịch vụ đã tồn tại.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(dichVu);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật dịch vụ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.DichVuPhuTro.Any(e => e.DichVuID == dichVu.DichVuID))
                        return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index), "AdminDichVu");
            }

            return View("~/Views/Admin/DichVu/DichVuEdit.cshtml", dichVu);
        }

        // GET: DichVu - Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var dichVu = await _context.DichVuPhuTro
                .FirstOrDefaultAsync(m => m.DichVuID == id);
            if (dichVu == null) return NotFound();
            return View("~/Views/Admin/DichVu/DichVuDelete.cshtml", dichVu);
        }

        // POST: DichVu - Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var dichVu = await _context.DichVuPhuTro.FindAsync(id);
            if (dichVu != null)
            {
                // Check if exists in DonDatSan before deleting
                var hasOrders = await _context.ChiTietDichVu
                    .Where(ctdv => ctdv.DichVuID == id)
                    .AnyAsync();

                if (hasOrders)
                {
                    TempData["Error"] = "Không thể xóa dịch vụ vì đã được sử dụng trong đơn đặt sân!";
                    return RedirectToAction(nameof(Index), "AdminDichVu");
                }

                _context.DichVuPhuTro.Remove(dichVu);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa dịch vụ thành công!";
            }

            return RedirectToAction(nameof(Index), "AdminDichVu");
        }
    }
}
