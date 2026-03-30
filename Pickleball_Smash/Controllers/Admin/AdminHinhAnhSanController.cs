using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;

namespace Pickleball_Smash.Controllers
{
    public class AdminHinhAnhSanController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

        public AdminHinhAnhSanController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: AdminHinhAnhSan?sanId=1
        public async Task<IActionResult> Index(int? sanId)
        {
            if (!sanId.HasValue)
            {
                TempData["Error"] = "Vui lòng chọn sân để quản lý ảnh.";
                return RedirectToAction("Index", "AdminSan");
            }

            var san = await _context.SanPickleball
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SanID == sanId.Value);

            if (san == null)
            {
                return NotFound();
            }

            var images = await _context.HinhAnhSan
                .AsNoTracking()
                .Where(x => x.SanID == sanId.Value)
                .OrderByDescending(x => x.HinhAnhID)
                .ToListAsync();

            ViewBag.SanId = san.SanID;
            ViewBag.TenSan = san.TenSan;
            return View("~/Views/Admin/HinhAnhSan/Index.cshtml", images);
        }

        // GET: AdminHinhAnhSan/Create?sanId=1
        public async Task<IActionResult> Create(int? sanId)
        {
            if (!sanId.HasValue)
            {
                TempData["Error"] = "Thiếu thông tin sân.";
                return RedirectToAction("Index", "AdminSan");
            }

            var san = await _context.SanPickleball
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SanID == sanId.Value);

            if (san == null)
            {
                return NotFound();
            }

            ViewBag.SanId = san.SanID;
            ViewBag.TenSan = san.TenSan;
            return View("~/Views/Admin/HinhAnhSan/Create.cshtml", new HinhAnhSan { SanID = san.SanID });
        }

        // POST: AdminHinhAnhSan/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SanID,DuongDanURL")] HinhAnhSan model, IFormFile? imageFile)
        {
            var san = model.SanID.HasValue
                ? await _context.SanPickleball.AsNoTracking().FirstOrDefaultAsync(x => x.SanID == model.SanID.Value)
                : null;

            if (!model.SanID.HasValue || san == null)
            {
                TempData["Error"] = "Sân không tồn tại.";
                return RedirectToAction("Index", "AdminSan");
            }

            if (imageFile == null && string.IsNullOrWhiteSpace(model.DuongDanURL))
            {
                ModelState.AddModelError("DuongDanURL", "Vui lòng nhập đường dẫn ảnh hoặc chọn file ảnh từ máy tính.");
            }

            if (imageFile != null)
            {
                if (!IsValidImageFile(imageFile))
                {
                    ModelState.AddModelError("DuongDanURL", "File ảnh không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .gif, .webp.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(model.DuongDanURL)
                && !Uri.TryCreate(model.DuongDanURL, UriKind.Absolute, out _)
                && !model.DuongDanURL.Trim().StartsWith('/'))
            {
                ModelState.AddModelError("DuongDanURL", "Đường dẫn ảnh không hợp lệ.");
            }

            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    model.DuongDanURL = await SaveUploadedImageAsync(imageFile);
                }
                else
                {
                    model.DuongDanURL = model.DuongDanURL!.Trim();
                }

                _context.HinhAnhSan.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm ảnh sân thành công.";
                return RedirectToAction(nameof(Index), new { sanId = model.SanID });
            }

            ViewBag.SanId = san.SanID;
            ViewBag.TenSan = san.TenSan;
            return View("~/Views/Admin/HinhAnhSan/Create.cshtml", model);
        }

        // GET: AdminHinhAnhSan/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var image = await _context.HinhAnhSan
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HinhAnhID == id.Value);

            if (image == null || !image.SanID.HasValue)
            {
                return NotFound();
            }

            var san = await _context.SanPickleball
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SanID == image.SanID.Value);

            if (san == null)
            {
                return NotFound();
            }

            ViewBag.SanId = san.SanID;
            ViewBag.TenSan = san.TenSan;
            return View("~/Views/Admin/HinhAnhSan/Edit.cshtml", image);
        }

        // POST: AdminHinhAnhSan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("HinhAnhID,SanID,DuongDanURL")] HinhAnhSan model, IFormFile? imageFile)
        {
            if (id != model.HinhAnhID)
            {
                return NotFound();
            }

            var existing = await _context.HinhAnhSan
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HinhAnhID == id);

            if (existing == null)
            {
                return NotFound();
            }

            var san = model.SanID.HasValue
                ? await _context.SanPickleball.AsNoTracking().FirstOrDefaultAsync(x => x.SanID == model.SanID.Value)
                : null;

            if (!model.SanID.HasValue || san == null)
            {
                TempData["Error"] = "Sân không tồn tại.";
                return RedirectToAction("Index", "AdminSan");
            }

            if (imageFile == null && string.IsNullOrWhiteSpace(model.DuongDanURL))
            {
                ModelState.AddModelError("DuongDanURL", "Vui lòng nhập đường dẫn ảnh hoặc chọn file ảnh từ máy tính.");
            }

            if (imageFile != null)
            {
                if (!IsValidImageFile(imageFile))
                {
                    ModelState.AddModelError("DuongDanURL", "File ảnh không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .gif, .webp.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(model.DuongDanURL)
                && !Uri.TryCreate(model.DuongDanURL, UriKind.Absolute, out _)
                && !model.DuongDanURL.Trim().StartsWith('/'))
            {
                ModelState.AddModelError("DuongDanURL", "Đường dẫn ảnh không hợp lệ.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null)
                    {
                        var oldPath = existing.DuongDanURL;
                        model.DuongDanURL = await SaveUploadedImageAsync(imageFile);
                        DeleteIfLocalUpload(oldPath);
                    }
                    else
                    {
                        model.DuongDanURL = model.DuongDanURL!.Trim();
                    }

                    _context.HinhAnhSan.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật ảnh sân thành công.";
                    return RedirectToAction(nameof(Index), new { sanId = model.SanID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    var exists = await _context.HinhAnhSan.AnyAsync(x => x.HinhAnhID == model.HinhAnhID);
                    if (!exists)
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            ViewBag.SanId = san.SanID;
            ViewBag.TenSan = san.TenSan;
            return View("~/Views/Admin/HinhAnhSan/Edit.cshtml", model);
        }

        // GET: AdminHinhAnhSan/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var image = await _context.HinhAnhSan
                .Include(x => x.SanPickleball)
                .FirstOrDefaultAsync(x => x.HinhAnhID == id.Value);

            if (image == null)
            {
                return NotFound();
            }

            return View("~/Views/Admin/HinhAnhSan/Delete.cshtml", image);
        }

        // POST: AdminHinhAnhSan/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var image = await _context.HinhAnhSan.FirstOrDefaultAsync(x => x.HinhAnhID == id);
            if (image != null)
            {
                var sanId = image.SanID;
                var oldPath = image.DuongDanURL;
                _context.HinhAnhSan.Remove(image);
                await _context.SaveChangesAsync();
                DeleteIfLocalUpload(oldPath);
                TempData["Success"] = "Xóa ảnh sân thành công.";
                return RedirectToAction(nameof(Index), new { sanId });
            }

            TempData["Error"] = "Không tìm thấy ảnh để xóa.";
            return RedirectToAction("Index", "AdminSan");
        }

        private bool IsValidImageFile(IFormFile imageFile)
        {
            if (imageFile.Length <= 0)
            {
                return false;
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            return AllowedExtensions.Contains(extension);
        }

        private async Task<string> SaveUploadedImageAsync(IFormFile imageFile)
        {
            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "san");
            Directory.CreateDirectory(uploadRoot);

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(uploadRoot, fileName);

            await using var stream = new FileStream(physicalPath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return $"/uploads/san/{fileName}";
        }

        private void DeleteIfLocalUpload(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/san/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}
