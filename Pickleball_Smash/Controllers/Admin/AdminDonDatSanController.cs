using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pickleball_Smash.Data;

namespace Pickleball_Smash.Controllers
{
    public class AdminDonDatSanController : Controller
    {
        private readonly AppDbContext _context;

        public AdminDonDatSanController(AppDbContext context)
        {
            _context = context;
        }

        // GET: DonDatSan - View only
        public async Task<IActionResult> Index()
        {
            var items = await _context.DonDatSan
                .Include(d => d.NguoiDung)
                .Include(d => d.SanPickleball)
                .Include(d => d.Voucher)
                .OrderByDescending(d => d.NgayTao)
                .ToListAsync();

            return View("~/Views/Admin/DonDatSan/Index.cshtml", items);
        }
    }
}
