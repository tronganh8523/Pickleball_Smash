using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class AdminVoucherController : Controller
    {
        private readonly AppDbContext _context;

        public AdminVoucherController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Voucher - List
        public async Task<IActionResult> Index()
        {
            var items = await _context.Voucher.ToListAsync();

            var now = DateTime.Now;
            var hasStatusChanges = false;
            foreach (var item in items)
            {
                if (item.NgayBatDau.HasValue && item.NgayKetThuc.HasValue)
                {
                    var calculated = CalculateVoucherStatus(item.NgayBatDau.Value, item.NgayKetThuc.Value, now);
                    if (item.TrangThai != calculated)
                    {
                        item.TrangThai = calculated;
                        hasStatusChanges = true;
                    }
                }
            }

            if (hasStatusChanges)
            {
                await _context.SaveChangesAsync();
            }

            return View("~/Views/Admin/Voucher/VoucherIndex.cshtml", items);
        }

        // GET: Voucher - Create
        public IActionResult Create()
        {
            return View("~/Views/Admin/Voucher/VoucherCreate.cshtml");
        }

        // POST: Voucher - Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaVoucher,MoTa,LoaiGiamGia,GiaTriGiam,GiamToiDa,GiaTriDonToiThieu,NgayBatDau,NgayKetThuc,SoLuongToiDa")] Voucher voucher)
        {
            // Khi tạo mới, số lượng đã dùng luôn mặc định bằng 0.
            voucher.SoLuongDaDung = 0;

            if (!string.IsNullOrWhiteSpace(voucher.MaVoucher))
            {
                var maVoucher = voucher.MaVoucher.Trim().ToLower();
                var exist = await _context.Voucher
                    .FirstOrDefaultAsync(x => x.MaVoucher.ToLower() == maVoucher);
                if (exist != null)
                {
                    ModelState.AddModelError("MaVoucher", "Mã voucher đã tồn tại.");
                }
            }

            ValidateVoucherDiscount(voucher);

            if (ModelState.IsValid)
            {
                voucher.TrangThai = CalculateVoucherStatus(voucher.NgayBatDau!.Value, voucher.NgayKetThuc!.Value, DateTime.Now);
                _context.Add(voucher);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm voucher thành công!";
                return RedirectToAction(nameof(Index), "AdminVoucher");
            }

            return View("~/Views/Admin/Voucher/VoucherCreate.cshtml", voucher);
        }

        // GET: Voucher - Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var voucher = await _context.Voucher.FindAsync(id);
            if (voucher == null) return NotFound();
            return View("~/Views/Admin/Voucher/VoucherEdit.cshtml", voucher);
        }

        // POST: Voucher - Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VoucherID,MaVoucher,MoTa,LoaiGiamGia,GiaTriGiam,GiamToiDa,GiaTriDonToiThieu,NgayBatDau,NgayKetThuc,SoLuongToiDa,SoLuongDaDung")] Voucher voucher)
        {
            if (id != voucher.VoucherID) return NotFound();

            if (!string.IsNullOrWhiteSpace(voucher.MaVoucher))
            {
                var maVoucher = voucher.MaVoucher.Trim().ToLower();
                var exist = await _context.Voucher
                    .FirstOrDefaultAsync(x => x.MaVoucher.ToLower() == maVoucher && x.VoucherID != voucher.VoucherID);
                if (exist != null)
                {
                    ModelState.AddModelError("MaVoucher", "Mã voucher đã tồn tại.");
                }
            }

            ValidateVoucherDiscount(voucher);

            if (ModelState.IsValid)
            {
                try
                {
                    voucher.TrangThai = CalculateVoucherStatus(voucher.NgayBatDau!.Value, voucher.NgayKetThuc!.Value, DateTime.Now);
                    _context.Update(voucher);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật voucher thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Voucher.Any(e => e.VoucherID == voucher.VoucherID))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index), "AdminVoucher");
            }

            return View("~/Views/Admin/Voucher/VoucherEdit.cshtml", voucher);
        }

        // GET: Voucher - Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var voucher = await _context.Voucher.FirstOrDefaultAsync(m => m.VoucherID == id);
            if (voucher == null) return NotFound();
            return View("~/Views/Admin/Voucher/VoucherDelete.cshtml", voucher);
        }

        // POST: Voucher - Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var voucher = await _context.Voucher.FindAsync(id);
            if (voucher != null)
            {
                _context.Voucher.Remove(voucher);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa voucher thành công!";
            }

            return RedirectToAction(nameof(Index), "AdminVoucher");
        }

        private void ValidateVoucherDiscount(Voucher voucher)
        {
            var loai = voucher.LoaiGiamGia?.Trim();

            if (!voucher.NgayBatDau.HasValue)
            {
                ModelState.AddModelError("NgayBatDau", "Ngày bắt đầu là bắt buộc.");
            }

            if (!voucher.NgayKetThuc.HasValue)
            {
                ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc là bắt buộc.");
            }

            if (voucher.NgayBatDau.HasValue && voucher.NgayKetThuc.HasValue)
            {
                if (voucher.NgayBatDau.Value >= voucher.NgayKetThuc.Value)
                {
                    ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");
                }
            }

            if (loai != "%" && loai != "VNĐ")
            {
                ModelState.AddModelError("LoaiGiamGia", "Loại giảm giá chỉ được chọn % hoặc VNĐ.");
                return;
            }

            if (!voucher.GiaTriGiam.HasValue)
            {
                ModelState.AddModelError("GiaTriGiam", "Giá trị giảm là bắt buộc.");
                return;
            }

            if (loai == "%")
            {
                if (voucher.GiaTriGiam.Value < 1 || voucher.GiaTriGiam.Value > 100)
                {
                    ModelState.AddModelError("GiaTriGiam", "Nếu loại giảm là %, giá trị giảm phải từ 1 đến 100.");
                }
            }
            else
            {
                if (voucher.GiaTriGiam.Value <= 0)
                {
                    ModelState.AddModelError("GiaTriGiam", "Nếu loại giảm là VNĐ, giá trị giảm phải lớn hơn 0.");
                }
            }
        }

        private static string CalculateVoucherStatus(DateTime ngayBatDau, DateTime ngayKetThuc, DateTime now)
        {
            if (now < ngayBatDau)
            {
                return "Sắp diễn ra";
            }

            if (now <= ngayKetThuc)
            {
                return "Đang hoạt động";
            }

            return "Hết hạn";
        }
    }
}