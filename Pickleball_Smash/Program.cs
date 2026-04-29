using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorOptions(options =>
{
    options.ViewLocationFormats.Add("/Views/User/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/Admin/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/Manager/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/Home/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/Shared/{0}.cshtml");
});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Thêm DbContext với SQL Server
builder.Services.AddDbContext<Pickleball_Smash.Data.AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Pickleball_Smash.Data.AppDbContext>();

    if (!db.NguoiDung.Any(x => x.TenDangNhap == "admin"))
    {
        db.NguoiDung.Add(new Pickleball_Smash.Models.NguoiDung
        {
            TenDangNhap = "admin",
            MatKhau = BCrypt.Net.BCrypt.HashPassword("1"),
            Email = "adminPickleballSmash@gmail.com",
            HoTen = "System Admin",
            VaiTro = "Admin",
            NgayTao = DateTime.Now
        });
    }

    if (!db.NguoiDung.Any(x => x.VaiTro == "User"))
    {
        db.NguoiDung.Add(new Pickleball_Smash.Models.NguoiDung
        {
            TenDangNhap = "user01",
            MatKhau = BCrypt.Net.BCrypt.HashPassword("1"),
            Email = "user01PickleballSmash@gmail.com",
            HoTen = "System User",
            VaiTro = "User",
            NgayTao = DateTime.Now
        });
    }

    if (!db.NguoiDung.Any(x => x.VaiTro == "Manager"))
    {
        db.NguoiDung.Add(new Pickleball_Smash.Models.NguoiDung
        {
            TenDangNhap = "manager01",
            MatKhau = BCrypt.Net.BCrypt.HashPassword("1"),
            Email = "manager01PickleballSmash@gmail.com",
            HoTen = "System Manager",
            VaiTro = "Manager",
            NgayTao = DateTime.Now
        });
    }

    db.SaveChanges();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
