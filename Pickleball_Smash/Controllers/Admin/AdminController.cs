using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;

namespace Pickleball_Smash.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var tongChiNhanh = await _context.ChiNhanh.CountAsync();
            var tongSan = await _context.SanPickleball.CountAsync();
            var tongNguoiDung = await _context.NguoiDung.CountAsync();
            var tongDonDat = await _context.DonDatSan.CountAsync();

            ViewBag.TongChiNhanh = tongChiNhanh;
            ViewBag.TongSan = tongSan;
            ViewBag.TongNguoiDung = tongNguoiDung;
            ViewBag.TongDonDat = tongDonDat;

            return View();
        }
    }
}