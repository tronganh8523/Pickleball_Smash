using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
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
            var items = await _context.SanPickleball
                .Include(s => s.ChiNhanh)
                .ToListAsync();
            return View("~/Views/Admin/San/SanIndex.cshtml", items);
        }

        // GET: San - Create
        public async Task<IActionResult> Create()
        {
            ViewBag.ChiNhanhs = await _context.ChiNhanh.ToListAsync();
            return View("~/Views/Admin/San/SanCreate.cshtml");
        }

        // POST: San - Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenSan,LoaiSan,MoTa,GiaCoBan,TrangThai,ChiNhanhID")] SanPickleball san)
        {
            // duplicate name within branch
            if (!string.IsNullOrWhiteSpace(san.TenSan))
            {
                var tenSan = san.TenSan.Trim().ToLower();
                var exist = await _context.SanPickleball
                    .FirstOrDefaultAsync(x => x.TenSan != null
                        && x.TenSan.ToLower() == tenSan
                        && x.ChiNhanhID == san.ChiNhanhID);
                if (exist != null)
                {
                    ModelState.AddModelError("TenSan", "Tên sân đã tồn tại trong chi nhánh này.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(san);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm sân thành công!";
                return RedirectToAction(nameof(Index), "AdminSan");
            }
            ViewBag.ChiNhanhs = await _context.ChiNhanh.ToListAsync();
            return View("~/Views/Admin/San/SanCreate.cshtml", san);
        }

        // GET: San - Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var san = await _context.SanPickleball.FindAsync(id);
            if (san == null) return NotFound();
            ViewBag.ChiNhanhs = await _context.ChiNhanh.ToListAsync();
            return View("~/Views/Admin/San/SanEdit.cshtml", san);
        }

        // POST: San - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SanID,TenSan,LoaiSan,MoTa,GiaCoBan,TrangThai,ChiNhanhID")] SanPickleball san)
        {
            if (id != san.SanID) return NotFound();
            if (!string.IsNullOrWhiteSpace(san.TenSan))
            {
                var tenSan = san.TenSan.Trim().ToLower();
                var exist = await _context.SanPickleball
                    .FirstOrDefaultAsync(x => x.TenSan != null
                        && x.TenSan.ToLower() == tenSan
                        && x.ChiNhanhID == san.ChiNhanhID
                        && x.SanID != san.SanID);
                if (exist != null)
                {
                    ModelState.AddModelError("TenSan", "Tên sân đã tồn tại trong chi nhánh này.");
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
            ViewBag.ChiNhanhs = await _context.ChiNhanh.ToListAsync();
            return View("~/Views/Admin/San/SanEdit.cshtml", san);
        }

        // GET: San - Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if(id==null) return NotFound();
            var san = await _context.SanPickleball
                .Include(s=>s.ChiNhanh)
                .FirstOrDefaultAsync(m=>m.SanID==id);
            if(san==null) return NotFound();
            return View("~/Views/Admin/San/SanDelete.cshtml", san);
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
    }
}

