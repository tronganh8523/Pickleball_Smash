// ================= BIẾN TOÀN CỤC =================
let currentBookingIds = [];
let finalPrice = 0;
let selectedSlots = [];
let bookedSlots = [];
let currentDetailCourtId = null;
let appliedVoucherCode = '';
let appliedDiscountAmount = 0;

function openModal(id) {
    document.querySelectorAll('.auth-modal-overlay').forEach(m => m.classList.remove('active'));
    document.getElementById(id).classList.add('active');
}

function closeModal(id) {
    document.getElementById(id).classList.remove('active');
}

document.addEventListener('DOMContentLoaded', async () => {
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

    const bkSan = document.getElementById('bkSan');
    const bkDate = document.getElementById('bkDate');
    const bkNote = document.getElementById('bkNote');

    if (bkSan) bkSan.addEventListener('change', fetchBookedSlots);
    if (bkDate) bkDate.addEventListener('change', fetchBookedSlots);
    if (bkNote) bkNote.addEventListener('input', calcTotal);

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
    if (appliedDiscountAmount > finalPrice) {
        appliedDiscountAmount = finalPrice;
    }

    const tongTienSauGiam = Math.max(0, finalPrice - appliedDiscountAmount);
    document.getElementById('sumTotalOrigin').innerText = finalPrice.toLocaleString('vi-VN') + 'đ';
    document.getElementById('sumTotalFinal').innerText = tongTienSauGiam.toLocaleString('vi-VN') + 'đ';
}

function resetVoucherState() {
    appliedVoucherCode = '';
    appliedDiscountAmount = 0;
    const voucherInput = document.getElementById('voucherCodeInput');
    if (voucherInput) {
        voucherInput.value = '';
    }
}

function applyVoucher() {
    const voucherInput = document.getElementById('voucherCodeInput');
    const voucherCode = voucherInput ? voucherInput.value.trim() : '';
    if (!voucherCode) {
        alert('Vui lòng nhập mã voucher.');
        return;
    }

    // Nếu đã có đơn, sử dụng endpoint cũ
    if (currentBookingIds.length > 0) {
        fetch('/San/ValidateVoucher', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                bookingIds: currentBookingIds,
                voucherCode: voucherCode
            })
        })
            .then(res => res.json())
            .then(data => {
                if (!data.success) {
                    alert(data.message || 'Không thể áp dụng voucher.');
                    return;
                }

                appliedVoucherCode = data.voucherCode || voucherCode;
                appliedDiscountAmount = Number(data.discountAmount || 0);
                document.getElementById('sumTotalOrigin').innerText = finalPrice.toLocaleString('vi-VN') + 'đ';
                document.getElementById('sumTotalFinal').innerText = Number(data.finalAmount || 0).toLocaleString('vi-VN') + 'đ';
                alert(data.message || 'Áp voucher thành công.');
            })
            .catch(() => alert('Không thể kiểm tra voucher lúc này.'));
    }
    // Nếu chưa tạo đơn nhưng đã chọn khung giờ, sử dụng endpoint mới (trước khi đặt sân)
    else if (selectedSlots.length > 0 && finalPrice > 0) {
        fetch('/San/ValidateVoucherBeforeBooking', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                voucherCode: voucherCode,
                totalAmount: finalPrice,
                bookingCount: selectedSlots.length
            })
        })
            .then(res => res.json())
            .then(data => {
                if (!data.success) {
                    alert(data.message || 'Không thể áp dụng voucher.');
                    return;
                }

                appliedVoucherCode = data.voucherCode || voucherCode;
                appliedDiscountAmount = Number(data.discountAmount || 0);
                document.getElementById('sumTotalOrigin').innerText = finalPrice.toLocaleString('vi-VN') + 'đ';
                document.getElementById('sumTotalFinal').innerText = Number(data.finalAmount || 0).toLocaleString('vi-VN') + 'đ';
                alert(data.message || 'Áp voucher thành công.');
            })
            .catch(() => alert('Không thể kiểm tra voucher lúc này.'));
    }
    else {
        alert('Vui lòng chọn ít nhất 1 khung giờ trước khi áp dụng voucher.');
        return;
    }
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
                // Không xóa appliedVoucherCode ở đây nữa - sẽ xóa sau khi thanh toán thành công
                closeModal('bookingModal');
                updatePaymentModal();
                openModal('paymentModal');
            } else {
                alert(data.message);
            }
        });
}

function updatePaymentModal() {
    const voucherInfo = document.getElementById('paymentVoucherInfo');
    const voucherCodeDisplay = document.getElementById('paymentVoucherCode');
    const voucherDiscountDisplay = document.getElementById('paymentVoucherDiscount');
    const finalAmountDisplay = document.getElementById('paymentFinalAmount');

    if (appliedVoucherCode && appliedDiscountAmount > 0) {
        voucherInfo.style.display = 'block';
        voucherCodeDisplay.innerText = appliedVoucherCode;
        voucherDiscountDisplay.innerText = appliedDiscountAmount.toLocaleString('vi-VN') + 'đ';
    } else {
        voucherInfo.style.display = 'none';
    }

    const finalAmount = Math.max(0, finalPrice - (appliedDiscountAmount || 0));
    finalAmountDisplay.innerText = finalAmount.toLocaleString('vi-VN') + 'đ';
}

// ================= LOGIC THANH TOÁN & LỊCH SỬ =================
function confirmPayment() {
    if (currentBookingIds.length === 0) return;

    fetch('/San/ConfirmPayment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            bookingIds: currentBookingIds,
            voucherCode: appliedVoucherCode || null
        })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                alert('Thanh toán thành công! Đơn của bạn sẽ được quản lý xác nhận.');
                currentBookingIds = [];
                resetVoucherState();
                closeModal('paymentModal');
                loadHistory();
            } else {
                alert(data.message || 'Thanh toán thất bại.');
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
            slot.innerHTML = `${i}:00 - ${i + 1}:00 <br> (Bận)`;
        } else {
            slot.classList.add('slot-available');
            slot.innerHTML = `${i}:00 - ${i + 1}:00 <br> (Trống)`;
        }
        grid.appendChild(slot);
    }
}

// ================= PROFILE & EDIT PROFILE =================
async function openProfileModal() {
    if (!window.isLoggedIn) return;

    const res = await fetch('/Account/GetProfile');
    if (res.ok) {
        const data = await res.json();
        document.getElementById('lblHoTen').innerText = data.hoTen || 'Chưa cập nhật';
        document.getElementById('lblEmail').innerText = data.email || 'Chưa cập nhật';
        document.getElementById('lblSDT').innerText = data.sdt || 'Chưa cập nhật';
        document.getElementById('lblGioiTinh').innerText = data.gioiTinh || 'Nam';
        document.getElementById('lblMaKH').innerText = data.maKhachHang;

        window.currentProfileData = data;
        openModal('profileModal');
    }
}

function openEditProfileModal() {
    closeModal('profileModal');

    const data = window.currentProfileData;
    if (data) {
        document.getElementById('updHoTen').value = data.hoTen || '';
        document.getElementById('updEmail').value = data.email || '';
        document.getElementById('updSDT').value = data.sdt || '';
        document.getElementById('updGioiTinh').value = data.gioiTinh || 'Nam';

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
}

// ================= AI CHATBOT LOGIC =================
document.addEventListener('DOMContentLoaded', () => {
    const toggleBtn = document.getElementById('chatbot-toggle');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggleChatbot);
    }

    const msgBox = document.getElementById('chatbot-messages');
    const chatWindow = document.getElementById('chatbot-window');

    const savedChat = sessionStorage.getItem('chatHistory');
    if (savedChat && msgBox) {
        msgBox.innerHTML = savedChat;
        msgBox.scrollTop = msgBox.scrollHeight;
    }

    const isChatOpen = sessionStorage.getItem('chatOpen');
    if (isChatOpen === 'true' && chatWindow) {
        chatWindow.style.display = 'flex';
    }
});

function toggleChatbot() {
    const chatWindow = document.getElementById('chatbot-window');
    if (chatWindow) {
        const isHidden = chatWindow.style.display === 'none';
        chatWindow.style.display = isHidden ? 'flex' : 'none';
        sessionStorage.setItem('chatOpen', isHidden ? 'true' : 'false');
    }
}

function handleChatKeyPress(e) {
    if (e.key === 'Enter') sendChatMessage();
}

function saveChatHistory() {
    const msgBox = document.getElementById('chatbot-messages');
    if (msgBox) {
        sessionStorage.setItem('chatHistory', msgBox.innerHTML);
    }
}

async function sendChatMessage() {
    const input = document.getElementById('chatInput');
    const message = input.value.trim();
    if (!message) return;

    const msgBox = document.getElementById('chatbot-messages');

    msgBox.innerHTML += `<div class="msg user-msg">${message}</div>`;
    input.value = '';
    msgBox.scrollTop = msgBox.scrollHeight;
    saveChatHistory();

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
        document.getElementById(typingId).outerHTML = `<div class="msg ai-msg">${data.reply}</div>`;
        msgBox.scrollTop = msgBox.scrollHeight;
        saveChatHistory();

    } catch (err) {
        document.getElementById(typingId).outerHTML = `<div class="msg ai-msg" style="color:red;">Lỗi kết nối. Vui lòng thử lại!</div>`;
        saveChatHistory();
    }
}

// ================= LOGIC LỊCH SỬ CHAT =================
let allChatSessions = [];
let currentChatSessions = [];

function loadChatHistory() {
    if (!window.isLoggedIn) {
        alert("Vui lòng đăng nhập để xem lịch sử chat!");
        return;
    }

    fetch('/Chatbot/GetChatSessions')
        .then(res => res.json())
        .then(data => {
            allChatSessions = data;
            openModal('chatHistoryModal');
            resetChatFilters();
        });
}

function filterAndSortChatHistory() {
    const dateVal = document.getElementById('chatDateFilter').value;
    const sortVal = document.getElementById('chatSortFilter').value;

    if (dateVal) {
        currentChatSessions = allChatSessions.filter(item => item.ngayGoc === dateVal);
    } else {
        currentChatSessions = [...allChatSessions];
    }

    if (sortVal === 'oldest') {
        currentChatSessions.sort((a, b) => new Date(a.ngayGoc) - new Date(b.ngayGoc));
    } else {
        currentChatSessions.sort((a, b) => new Date(b.ngayGoc) - new Date(a.ngayGoc));
    }

    renderChatSessions();
}

function resetChatFilters() {
    const dateFilter = document.getElementById('chatDateFilter');
    const sortFilter = document.getElementById('chatSortFilter');

    if (dateFilter) dateFilter.value = '';
    if (sortFilter) sortFilter.value = 'newest';

    filterAndSortChatHistory();
}

function renderChatSessions() {
    const tbody = document.getElementById('chatHistoryTableBody');
    tbody.innerHTML = '';

    if (currentChatSessions.length === 0) {
        tbody.innerHTML = `<tr><td colspan="3" style="text-align:center; padding: 20px;">Không tìm thấy phiên chat nào phù hợp.</td></tr>`;
    } else {
        currentChatSessions.forEach(item => {
            tbody.innerHTML += `
                <tr>
                    <td>
                        Phiên ngày: <strong>${item.ngayHienThi}</strong><br>
                        <span style="font-size: 0.85rem; color: #666;">
                            <i class="far fa-clock"></i> ${item.thoiGianBatDau} - ${item.thoiGianKetThuc}
                        </span>
                    </td>
                    <td style="vertical-align: middle;">${item.soTinNhan} tương tác</td>
                    <td style="vertical-align: middle;">
                        <button class="btn-primary-blue" style="padding: 5px 15px; font-size: 14px;" 
                                onclick="openChatDetail('${item.ngayGoc}', '${item.thoiGianBatDau}', '${item.thoiGianKetThuc}')">
                            <i class="fas fa-eye"></i> Xem nội dung
                        </button>
                    </td>
                </tr>`;
        });
    }
}

function openChatDetail(dateString, startString, endString) {
    fetch(`/Chatbot/GetChatDetails?date=${dateString}&start=${startString}&end=${endString}`)
        .then(res => res.json())
        .then(data => {
            const contentBox = document.getElementById('chatDetailContent');
            contentBox.innerHTML = '';

            data.forEach(msg => {
                contentBox.innerHTML += `
                    <div class="msg user-msg" style="margin-top: 10px;">
                        ${msg.hoi} <br>
                        <small style="opacity: 0.7; font-size: 10px;">${msg.thoiGianGian}</small>
                    </div>`;

                contentBox.innerHTML += `
                    <div class="msg ai-msg">
                        ${msg.dap} <br>
                        <small style="opacity: 0.7; font-size: 10px;">${msg.thoiGianGian}</small>
                    </div>`;
            });

            closeModal('chatHistoryModal');
            openModal('chatDetailModal');
            contentBox.scrollTop = contentBox.scrollHeight;
        });
}