using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;
using Pickleball_Smash.Models;
using System.Text.Json;

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
        public IActionResult Create(int? sanId)
        {
            TempData["Error"] = "Vui lòng thao tác tạo ảnh bằng popup tại trang danh sách ảnh.";
            if (sanId.HasValue)
            {
                return RedirectToAction(nameof(Index), new { sanId = sanId.Value });
            }

            return RedirectToAction("Index", "AdminSan");
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

            SetModalState("create-hinhanh", model);
            return RedirectToAction(nameof(Index), new { sanId = model.SanID });
        }

        // GET: AdminHinhAnhSan/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            TempData["Error"] = "Vui lòng thao tác chỉnh sửa ảnh bằng popup tại trang danh sách ảnh.";

            if (id.HasValue)
            {
                var image = await _context.HinhAnhSan
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HinhAnhID == id.Value);

                if (image?.SanID.HasValue == true)
                {
                    return RedirectToAction(nameof(Index), new { sanId = image.SanID.Value });
                }
            }

            return RedirectToAction("Index", "AdminSan");
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

            SetModalState("edit-hinhanh", model);
            return RedirectToAction(nameof(Index), new { sanId = model.SanID });
        }

        // GET: AdminHinhAnhSan/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            TempData["Error"] = "Chức năng xóa trực tiếp bằng popup, không dùng trang Delete riêng.";

            if (id.HasValue)
            {
                var image = await _context.HinhAnhSan
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HinhAnhID == id.Value);

                if (image?.SanID.HasValue == true)
                {
                    return RedirectToAction(nameof(Index), new { sanId = image.SanID.Value });
                }
            }

            return RedirectToAction("Index", "AdminSan");
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
