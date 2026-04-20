CREATE DATABASE PickleballSmash_DB;
GO

USE PickleballSmash_DB;
GO

-- =======================================================
-- TẠO CẤU TRÚC CÁC BẢNG (TABLES)
-- =======================================================

-- 1. Bảng Người Dùng (Khách hàng & Nhân viên)
CREATE TABLE NguoiDung (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Password NVARCHAR(255) NOT NULL,
    HoTen NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    SDT VARCHAR(20) NOT NULL,
    GioiTinh NVARCHAR(10) NULL,
    Role NVARCHAR(20) DEFAULT 'KhachHang', -- 'KhachHang' hoặc 'NhanVien'
    MaKhachHang VARCHAR(20) UNIQUE NULL,
    NgayTao DATETIME DEFAULT GETDATE()
);
GO

-- 2. Bảng Sân Pickleball
CREATE TABLE SanPickleball (
    SanID INT IDENTITY(1,1) PRIMARY KEY,
    TenSan NVARCHAR(100) NOT NULL,
    LoaiSan NVARCHAR(50) NOT NULL, -- 'Ngoài trời' hoặc 'Trong nhà/VIP'
    GiaCoBan DECIMAL(18,2) NOT NULL,
    MoTa NVARCHAR(500) NULL,
    HinhAnh NVARCHAR(255) NULL,
    TrangThai NVARCHAR(50) DEFAULT N'Đang hoạt động'
);
GO

-- 3. Bảng Đặt Sân (Hóa đơn)
CREATE TABLE DatSan (
    BookingID INT IDENTITY(1,1) PRIMARY KEY,
    MaHoaDon VARCHAR(50) UNIQUE NOT NULL,
    NguoiDungID INT NOT NULL,
    SanID INT NOT NULL,
    NgayDat DATE NOT NULL,
    KhungGio NVARCHAR(255) NOT NULL, -- Lưu chuỗi khung giờ, VD: "11,12" hoặc "11:00 - 13:00"
    GhiChu NVARCHAR(500) NULL,
    TongTien DECIMAL(18,2) NOT NULL,
    TrangThai NVARCHAR(50) DEFAULT N'Chờ thanh toán', -- 'Chờ thanh toán', 'Đã thanh toán', 'Đã hủy'
    NgayThanhToan DATETIME NULL,
    ThoiGianTao DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_DatSan_NguoiDung FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(UserID),
    CONSTRAINT FK_DatSan_San FOREIGN KEY (SanID) REFERENCES SanPickleball(SanID)
);
GO

-- 4. Bảng Lịch Sử Chat AI (Dùng cho Chatbot)
CREATE TABLE LichSuChat (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NULL, -- Cho phép null nếu khách chưa đăng nhập vẫn chat
    NoiDungHoi NVARCHAR(MAX) NOT NULL,
    PhanHoiAI NVARCHAR(MAX) NOT NULL,
    ThoiGian DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_LichSuChat_NguoiDung FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(UserID)
);
GO

-- =======================================================
-- CHÈN DỮ LIỆU MẪU BAN ĐẦU (SEED DATA)
-- =======================================================

-- Thêm tài khoản Admin/Nhân viên và 1 Khách hàng mẫu
INSERT INTO NguoiDung (Username, Password, HoTen, Email, SDT, NgaySinh, GioiTinh, Role, MaKhachHang)
VALUES 
('admin', '123456', N'Quản trị viên', 'admin@picklesmash.com', '0999999999', '1990-01-01', N'Nam', 'NhanVien', 'NV001'),
('khachhang1', '123456', N'Nguyễn Văn A', 'nva@gmail.com', '0912345678', '2000-05-15', N'Nam', 'KhachHang', 'KH001');
GO

-- Thêm danh sách sân Pickleball thực tế
INSERT INTO SanPickleball (TenSan, LoaiSan, GiaCoBan, MoTa, HinhAnh)
VALUES 
(N'Sân Ngoài Trời 01', N'Ngoài trời', 150000.00, N'Sân tiêu chuẩn ngoài trời, mặt sân thảm nhựa tổng hợp bám giày tốt, thoáng mát.', '/Img/san_ngoaitroi1.jpg'),
(N'Sân Ngoài Trời 02', N'Ngoài trời', 150000.00, N'Sân ngoài trời khu vực trung tâm, ánh sáng đèn pha LED tiêu chuẩn cho buổi tối.', '/Img/san_ngoaitroi2.jpg'),
(N'Sân Trong Nhà VIP 01', N'Trong nhà', 300000.00, N'Sân VIP có mái che, điều hòa làm mát, mặt sân gỗ/thảm cao cấp, có khu vực nghỉ ngơi riêng.', '/Img/san_trongnha1.jpg'),
(N'Sân Trong Nhà VIP 02', N'Trong nhà', 300000.00, N'Sân kín gió, không sợ thời tiết, phục vụ nước uống miễn phí và khăn lạnh.', '/Img/san_trongnha2.jpg');
GO

-- Thêm một vài dòng lịch sử chat mẫu để test tính năng phân trang/phân phiên
INSERT INTO LichSuChat (NguoiDungID, NoiDungHoi, PhanHoiAI, ThoiGian)
VALUES 
(2, N'xin chào', N'Chào Anh/Chị! Em là trợ lý của Pickleball Smash. Em có thể hỗ trợ gì cho Anh/Chị hôm nay ạ?', DATEADD(day, -1, GETDATE())),
(2, N'sân ngoài trời giá bao nhiêu', N'Dạ, sân Ngoài trời bên em có mức giá là 150.000đ/giờ ạ.', DATEADD(day, -1, GETDATE())),
(2, N'ok cảm ơn', N'Dạ, nếu cần thêm thông tin gì Anh/Chị cứ nhắn em nhé. Chúc Anh/Chị một ngày vui vẻ!', DATEADD(day, -1, GETDATE()));
GO

PRINT N'Cài đặt Database PickleballSmash_DB thành công!';