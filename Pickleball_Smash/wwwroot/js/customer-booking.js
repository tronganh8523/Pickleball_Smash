// ================= BIẾN TOÀN CỤC =================
let currentBookingIds = [];
let finalPrice = 0;
let selectedSlots = [];
let bookedSlots = [];
let currentDetailCourtId = null;

// Hàm mở / đóng Modal dùng chung
function openModal(id) {
    document.querySelectorAll('.auth-modal-overlay').forEach(m => m.classList.remove('active'));
    document.getElementById(id).classList.add('active');
}

function closeModal(id) {
    document.getElementById(id).classList.remove('active');
}

document.addEventListener('DOMContentLoaded', async () => {
    // 1. Gán sự kiện mở form Đặt Sân
    document.querySelectorAll('.btn-book').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.preventDefault();
            const courtId = e.target.getAttribute('data-id');
            if (courtId) document.getElementById('bkSan').value = courtId;

            if (!document.getElementById('bkDate').value) {
                document.getElementById('bkDate').value = new Date().toISOString().split('T')[0];
            }

            openModal('bookingModal');
            await fetchBookedSlots();
        });
    });

    // 2. Gán sự kiện cho Nút Lịch Sử trên Header
    document.addEventListener('click', (e) => {
        if (e.target.innerText.includes('Lịch Sử')) loadHistory();
    });

    // 3. Gán sự kiện mở form Xem Chi Tiết
    document.querySelectorAll('.btn-detail').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.preventDefault();
            const btnData = e.currentTarget;
            currentDetailCourtId = btnData.getAttribute('data-id');

            document.getElementById('dtName').innerText = btnData.getAttribute('data-name');
            document.getElementById('dtType').innerText = btnData.getAttribute('data-type');
            document.getElementById('dtPrice').innerText = btnData.getAttribute('data-price');
            document.getElementById('dtDesc').innerText = btnData.getAttribute('data-desc');
            document.getElementById('dtImg').src = btnData.getAttribute('data-img');

            if (!document.getElementById('dtDate').value) {
                document.getElementById('dtDate').value = new Date().toISOString().split('T')[0];
            }

            openModal('detailModal');
            await fetchDetailSlots();
        });
    });

    // 4. Các sự kiện thay đổi Input
    const bkSan = document.getElementById('bkSan');
    const bkDate = document.getElementById('bkDate');
    const bkNote = document.getElementById('bkNote');

    if (bkSan) bkSan.addEventListener('change', fetchBookedSlots);
    if (bkDate) bkDate.addEventListener('change', fetchBookedSlots);
    if (bkNote) bkNote.addEventListener('input', calcTotal);

    // Xử lý nút Tiến hành đặt sân từ popup Chi tiết
    const dtBtnBook = document.getElementById('dtBtnBook');
    if (dtBtnBook) {
        dtBtnBook.addEventListener('click', async () => {
            closeModal('detailModal');
            document.getElementById('bkSan').value = currentDetailCourtId;
            document.getElementById('bkDate').value = document.getElementById('dtDate').value;
            openModal('bookingModal');
            await fetchBookedSlots();
        });
    }

    // 5. Phục hồi dữ liệu Đặt sân sau khi Đăng nhập
    const pendingStr = sessionStorage.getItem('pendingBooking');
    if (pendingStr && window.isLoggedIn) {
        const data = JSON.parse(pendingStr);
        document.getElementById('bkSan').value = data.SanID;
        document.getElementById('bkDate').value = data.NgayDat.split('T')[0];
        document.getElementById('bkNote').value = data.GhiChu;

        openModal('bookingModal');
        await fetchBookedSlots();
        selectedSlots = data.SelectedHours.filter(s => !bookedSlots.includes(s));
        renderTimeSlots();
        calcTotal();
        sessionStorage.removeItem('pendingBooking');
    }
});

// ================= LOGIC ĐẶT SÂN & KHUNG GIỜ =================
async function fetchBookedSlots() {
    const sanId = document.getElementById('bkSan').value;
    const date = document.getElementById('bkDate').value;
    if (!sanId || !date) return;

    const res = await fetch(`/San/GetBookedSlots?sanId=${sanId}&date=${date}`);
    bookedSlots = await res.json();
    selectedSlots = selectedSlots.filter(s => !bookedSlots.includes(s));

    renderTimeSlots();
    calcTotal();
}

function renderTimeSlots() {
    const grid = document.getElementById('timeSlotGrid');
    if (!grid) return;
    grid.innerHTML = '';

    for (let i = 5; i < 22; i++) {
        const slot = document.createElement('div');
        slot.className = 'time-slot';
        if (bookedSlots.includes(i)) slot.classList.add('disabled');
        else if (selectedSlots.includes(i)) slot.classList.add('selected');

        slot.innerText = `${i}:00 - ${i + 1}:00`;
        slot.onclick = () => toggleSlot(i);
        grid.appendChild(slot);
    }
}

function toggleSlot(hour) {
    if (bookedSlots.includes(hour)) return;
    const idx = selectedSlots.indexOf(hour);
    if (idx > -1) selectedSlots.splice(idx, 1);
    else selectedSlots.push(hour);

    selectedSlots.sort((a, b) => a - b);
    renderTimeSlots();
    calcTotal();
}

function calcTotal() {
    const sanSelect = document.getElementById('bkSan');
    if (!sanSelect || sanSelect.selectedIndex === -1) return;

    const opt = sanSelect.options[sanSelect.selectedIndex];
    const pricePerHour = opt.value ? parseFloat(opt.getAttribute('data-price')) : 0;
    const name = opt.value ? opt.getAttribute('data-name') : '...';
    const type = opt.value ? opt.getAttribute('data-type') : '...';

    document.getElementById('sumDate').innerText = document.getElementById('bkDate').value || '...';
    document.getElementById('sumType').innerText = `${name} - ${type}`;
    document.getElementById('sumNote').innerText = document.getElementById('bkNote').value || 'Không có';

    if (selectedSlots.length > 0) {
        document.getElementById('sumTime').innerText = selectedSlots.map(h => `${h}:00 - ${h + 1}:00`).join(', ');
    } else {
        document.getElementById('sumTime').innerText = '...';
    }

    finalPrice = pricePerHour * selectedSlots.length;
    const priceStr = finalPrice.toLocaleString('vi-VN') + 'đ';
    document.getElementById('sumTotalOrigin').innerText = priceStr;
    document.getElementById('sumTotalFinal').innerText = priceStr;
}

function submitBooking() {
    if (!document.getElementById('bkDate').value || !document.getElementById('bkSan').value || selectedSlots.length === 0) {
        alert('Vui lòng chọn ngày, sân và ít nhất 1 khung giờ hợp lệ!');
        return;
    }

    const bookingData = {
        SanID: parseInt(document.getElementById('bkSan').value),
        NgayDat: document.getElementById('bkDate').value,
        SelectedHours: selectedSlots,
        GhiChu: document.getElementById('bkNote').value,
        TongTien: finalPrice
    };

    if (!window.isLoggedIn) {
        sessionStorage.setItem('pendingBooking', JSON.stringify(bookingData));
        closeModal('bookingModal');
        if (typeof openAuthModal === 'function') openAuthModal('login');
        return;
    }

    fetch('/San/CreateBooking', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(bookingData)
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                currentBookingIds = data.bookingIds;
                closeModal('bookingModal');
                openModal('paymentModal');
            } else {
                alert(data.message);
            }
        });
}

// ================= LOGIC THANH TOÁN & LỊCH SỬ =================
function confirmPayment() {
    if (currentBookingIds.length === 0) return;

    fetch('/San/ConfirmPayment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(currentBookingIds)
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                alert('Thanh toán thành công!');
                closeModal('paymentModal');
                loadHistory();
            }
        });
}

function loadHistory() {
    if (!window.isLoggedIn) { alert("Vui lòng đăng nhập!"); return; }
    fetch('/San/GetBookingHistory').then(res => res.json()).then(data => {
        const tbody = document.getElementById('historyTableBody');
        if (!tbody) return;
        tbody.innerHTML = '';
        data.forEach(item => {
            tbody.innerHTML += `
                <tr>
                    <td>${item.maHoaDon}</td><td>${item.ngayThanhToan}</td>
                    <td>${item.loaiSan}</td><td>${item.khungGio}</td>
                    <td>${item.tongTien.toLocaleString('vi-VN')}đ</td>
                    <td><span class="status-paid">${item.trangThai}</span></td>
                </tr>`;
        });
        openModal('historyModal');
    });
}

// ================= LOGIC CHI TIẾT SÂN =================
async function fetchDetailSlots() {
    const date = document.getElementById('dtDate').value;
    if (!currentDetailCourtId || !date) return;

    const res = await fetch(`/San/GetBookedSlots?sanId=${currentDetailCourtId}&date=${date}`);
    const dtBookedSlots = await res.json();

    const grid = document.getElementById('dtTimeSlotGrid');
    if (!grid) return;
    grid.innerHTML = '';

    for (let i = 5; i < 22; i++) {
        const slot = document.createElement('div');
        slot.className = 'time-slot';
        if (dtBookedSlots.includes(i)) {
            slot.classList.add('slot-busy');
            // Đổi innerText thành innerHTML và thêm <br>
            slot.innerHTML = `${i}:00 - ${i + 1}:00 <br> (Bận)`;
        } else {
            slot.classList.add('slot-available');
            // Đổi innerText thành innerHTML và thêm <br>
            slot.innerHTML = `${i}:00 - ${i + 1}:00 <br> (Trống)`;
        }
        grid.appendChild(slot);
    }
}

// ================= PROFILE & EDIT PROFILE =================
async function openProfileModal() {
    if (!window.isLoggedIn) return;

    // Fetch dữ liệu từ server
    const res = await fetch('/Account/GetProfile');
    if (res.ok) {
        const data = await res.json();
        // Hiển thị lên Popup Thông tin cá nhân
        document.getElementById('lblHoTen').innerText = data.hoTen || 'Chưa cập nhật';
        document.getElementById('lblEmail').innerText = data.email || 'Chưa cập nhật';
        document.getElementById('lblSDT').innerText = data.sdt || 'Chưa cập nhật';
        document.getElementById('lblNgaySinh').innerText = data.ngaySinh ? data.ngaySinh.split('-').reverse().join('/') : 'Chưa cập nhật';
        document.getElementById('lblGioiTinh').innerText = data.gioiTinh || 'Nam';
        document.getElementById('lblMaKH').innerText = data.maKhachHang;

        // Lưu tạm data để điền vào form Edit nếu khách bấm chỉnh sửa
        window.currentProfileData = data;

        openModal('profileModal');
    }
}

function openEditProfileModal() {
    closeModal('profileModal');

    // Đổ dữ liệu có sẵn vào các ô input
    const data = window.currentProfileData;
    if (data) {
        document.getElementById('updHoTen').value = data.hoTen || '';
        document.getElementById('updEmail').value = data.email || '';
        document.getElementById('updSDT').value = data.sdt || '';
        document.getElementById('updNgaySinh').value = data.ngaySinh || '';
        document.getElementById('updGioiTinh').value = data.gioiTinh || 'Nam';

        // Reset password fields
        document.getElementById('updOldPass').value = '';
        document.getElementById('updNewPass').value = '';
        document.getElementById('updConfirmPass').value = '';
    }

    openModal('editProfileModal');
}

async function submitUpdateProfile() {
    const newPass = document.getElementById('updNewPass').value;
    const confirmPass = document.getElementById('updConfirmPass').value;

    if (newPass && newPass !== confirmPass) {
        alert("Mật khẩu xác nhận không khớp!");
        return;
    }

    const payload = {
        FullName: document.getElementById('updHoTen').value,
        Email: document.getElementById('updEmail').value,
        Phone: document.getElementById('updSDT').value,
        Dob: document.getElementById('updNgaySinh').value,
        Gender: document.getElementById('updGioiTinh').value,
        OldPassword: document.getElementById('updOldPass').value,
        NewPassword: newPass
    };

    try {
        const res = await fetch('/Account/UpdateProfile', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const result = await res.json();
        if (result.success) {
            alert(result.message);
            closeModal('editProfileModal');
            window.location.reload();
        } else {
            alert(result.message);
        }
    } catch (err) {
        alert("Có lỗi xảy ra, vui lòng thử lại sau.");
    }
} // <-- DẤU NGOẶC NÀY RẤT QUAN TRỌNG ĐỂ ĐÓNG HÀM UPDATE PROFILE

// ================= AI CHATBOT LOGIC =================
// Đưa logic chatbot ra ngoài cùng để nó hoạt động độc lập ngay khi tải trang
document.addEventListener('DOMContentLoaded', () => {
    const toggleBtn = document.getElementById('chatbot-toggle');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggleChatbot);
    }
});

function toggleChatbot() {
    const chatWindow = document.getElementById('chatbot-window');
    if (chatWindow) {
        chatWindow.style.display = chatWindow.style.display === 'none' ? 'flex' : 'none';
    }
}

function handleChatKeyPress(e) {
    if (e.key === 'Enter') sendChatMessage();
}

async function sendChatMessage() {
    const input = document.getElementById('chatInput');
    const message = input.value.trim();
    if (!message) return;

    const msgBox = document.getElementById('chatbot-messages');

    // Hiển thị tin nhắn của user
    msgBox.innerHTML += `<div class="msg user-msg">${message}</div>`;
    input.value = '';
    msgBox.scrollTop = msgBox.scrollHeight;

    // Hiển thị trạng thái đang gõ
    const typingId = 'typing-' + Date.now();
    msgBox.innerHTML += `<div id="${typingId}" class="msg ai-msg">Đang suy nghĩ...</div>`;
    msgBox.scrollTop = msgBox.scrollHeight;

    try {
        const res = await fetch('/Chatbot/SendMessage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userMessage: message })
        });

        const data = await res.json();

        // Thay thế "Đang suy nghĩ" bằng câu trả lời thật
        document.getElementById(typingId).outerHTML = `<div class="msg ai-msg">${data.reply}</div>`;
        msgBox.scrollTop = msgBox.scrollHeight;

    } catch (err) {
        document.getElementById(typingId).outerHTML = `<div class="msg ai-msg" style="color:red;">Lỗi kết nối. Vui lòng thử lại!</div>`;
    }
}