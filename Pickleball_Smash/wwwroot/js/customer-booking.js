// ================= BIẾN TOÀN CỤC =================
let currentPaymentType = 'Full';
let currentBookingIds = [];
let finalPrice = 0;
let selectedSlots = [];
let bookedSlots = [];
let currentDetailCourtId = null;
let appliedVoucherCode = '';
let appliedDiscountAmount = 0;
let userHistoryData = [];
let currentCourtPrices = {};
let isEditMode = false;
let editTargetBookingId = 0;

function openModal(id) {
    document.querySelectorAll('.auth-modal-overlay').forEach(m => m.classList.remove('active'));
    document.getElementById(id).classList.add('active');
}

function closeModal(id) {
    document.getElementById(id).classList.remove('active');
}

document.addEventListener('DOMContentLoaded', async () => {
    const todayStr = new Date().toISOString().split('T')[0];
    const bkDateInput = document.getElementById('bkDate');
    const dtDateInput = document.getElementById('dtDate');
    if (bkDateInput) bkDateInput.setAttribute('min', todayStr);
    if (dtDateInput) dtDateInput.setAttribute('min', todayStr);
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

            if (!currentDetailCourtId) return;
            e.preventDefault();

            // Lấy trạng thái từ thuộc tính data-status đã thêm ở bước 2
            const status = btnData.getAttribute('data-status');
            const dtBtnBook = document.getElementById('dtBtnBook');
            if (dtBtnBook) {
                if (status === "Bận") {
                    dtBtnBook.innerText = "Sân đang bận";
                    dtBtnBook.disabled = true;
                    dtBtnBook.classList.add('disabled');
                } else {
                    dtBtnBook.innerText = "Đặt Sân";
                    dtBtnBook.disabled = false;
                    dtBtnBook.classList.remove('disabled');
                }
            }

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

    // Gọi song song 2 API: Lấy giờ bận & Lấy bảng giá tùy chỉnh
    const [resBooked, resPrices] = await Promise.all([
        fetch(`/San/GetBookedSlots?sanId=${sanId}&date=${date}`),
        fetch(`/San/GetCourtCustomPrices?sanId=${sanId}`)
    ]);

    bookedSlots = await resBooked.json();
    currentCourtPrices = await resPrices.json(); // Nhận bảng giá từ C#

    selectedSlots = selectedSlots.filter(s => !bookedSlots.includes(s));

    renderTimeSlots();
    calcTotal();
}

// Thay thế hàm renderTimeSlots hiện tại trong customer-booking.js
function renderTimeSlots() {
    const grid = document.getElementById('timeSlotGrid');
    if (!grid) return;
    grid.innerHTML = '';

    // Lấy ngày chọn và ngày giờ hiện tại
    const selectedDate = document.getElementById('bkDate').value;
    const today = new Date();
    // Chuyển today về định dạng YYYY-MM-DD theo múi giờ local
    const todayStr = today.getFullYear() + '-' + String(today.getMonth() + 1).padStart(2, '0') + '-' + String(today.getDate()).padStart(2, '0');

    const isToday = selectedDate === todayStr;
    const currentHour = today.getHours();

    for (let i = 5; i < 24; i++) {
        const slot = document.createElement('div');
        slot.className = 'time-slot';

        // Logic chặn giờ quá khứ
        const isPastHour = isToday && (i <= currentHour);

        if (bookedSlots.includes(i)) {
            slot.classList.add('disabled');
            slot.innerText = `${i}:00 - ${i + 1}:00`;
        }
        else if (isPastHour) {
            slot.classList.add('disabled');
            slot.innerText = `${i}:00 - ${i + 1}:00\n(Đã qua)`;
        }
        else if (selectedSlots.includes(i)) {
            slot.classList.add('selected');
            slot.innerText = `${i}:00 - ${i + 1}:00`;
        }
        else {
            slot.innerText = `${i}:00 - ${i + 1}:00`;
        }

        // Chỉ cho phép click nếu không bị disabled
        if (!slot.classList.contains('disabled')) {
            slot.onclick = () => toggleSlot(i);
        }
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
    const basePrice = opt.value ? parseFloat(opt.getAttribute('data-price')) : 0;
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

    // --- THUẬT TOÁN TÍNH TIỀN MỚI ---
    finalPrice = 0;
    selectedSlots.forEach(hour => {
        const hourStr = hour.toString();
        // Kiểm tra xem khung giờ này có được Admin setup giá riêng không
        if (currentCourtPrices && currentCourtPrices[hourStr] !== undefined) {
            finalPrice += parseFloat(currentCourtPrices[hourStr]);
        } else {
            // Nếu không có, dùng giá cơ bản của sân
            finalPrice += basePrice;
        }
    });
    // -------------------------------

    if (appliedDiscountAmount > finalPrice) {
        appliedDiscountAmount = finalPrice;
    }

    const tongTienSauGiam = Math.max(0, finalPrice - appliedDiscountAmount);
    document.getElementById('sumTotalOrigin').innerText = finalPrice.toLocaleString('vi-VN') + 'đ';
    document.getElementById('sumTotalFinal').innerText = tongTienSauGiam.toLocaleString('vi-VN') + 'đ';
    const discountRow = document.getElementById('discountDisplayRow');
    const discountSpan = document.getElementById('sumDiscountAmount');
    if (appliedDiscountAmount > 0) {
        if (discountRow) discountRow.style.display = 'flex';
        if (discountSpan) discountSpan.innerText = '-' + appliedDiscountAmount.toLocaleString('vi-VN') + 'đ';
    } else {
        if (discountRow) discountRow.style.display = 'none';
    }
}

function resetVoucherState() {
    appliedVoucherCode = '';
    appliedDiscountAmount = 0;
    const voucherInput = document.getElementById('voucherCodeInput');
    if (voucherInput) {
        voucherInput.value = '';
    }
    const discountRow = document.getElementById('discountDisplayRow');
    if (discountRow) discountRow.style.display = 'none';
}

function applyVoucher() {
    const voucherInput = document.getElementById('voucherCodeInput');
    const voucherCode = voucherInput ? voucherInput.value.trim() : '';
    if (!voucherCode) {
        alert('Vui lòng nhập mã voucher.');
        return;
    }

    if (currentBookingIds.length > 0) {
        fetch('/San/ValidateVoucher', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ bookingIds: currentBookingIds, voucherCode: voucherCode })
        })
            .then(res => res.json())
            .then(data => {
                if (!data.success) {
                    alert(data.message || 'Không thể áp dụng voucher.');
                    return;
                }

                appliedVoucherCode = data.voucherCode || voucherCode;
                appliedDiscountAmount = Number(data.discountAmount || 0);

                // Tự động gọi lại hàm tính tiền để update toàn bộ giao diện
                calcTotal();

                alert(data.message || 'Áp voucher thành công.');
            })
            .catch(() => alert('Không thể kiểm tra voucher lúc này.'));
    }
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

                // Tự động gọi lại hàm tính tiền để update toàn bộ giao diện
                calcTotal();

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
        TongTien: finalPrice,
        BookingID: typeof editTargetBookingId !== 'undefined' ? editTargetBookingId : 0
    };

    // ================= CHẾ ĐỘ SỬA ĐƠN =================
    if (isEditMode) {
        fetch('/San/RequestEditBooking', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(bookingData)
        })
            .then(res => res.json())
            .then(data => {
                alert(data.message);
                if (data.success) {
                    closeBookingModal(); // Đóng và reset form
                    loadHistory();       // Tự động load lại bảng lịch sử
                }
            })
            .catch(err => alert('Lỗi kết nối máy chủ!'));

        return; // DỪNG HÀM TẠI ĐÂY, KHÔNG CHẠY PHẦN ĐẶT MỚI BÊN DƯỚI
    }

    // ================= CHẾ ĐỘ ĐẶT SÂN MỚI =================
    if (!window.isLoggedIn) {
        sessionStorage.setItem('pendingBooking', JSON.stringify(bookingData));
        closeBookingModal();
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
                closeBookingModal();

                // Tính tiền và đổ vào popup chọn hình thức thanh toán
                const finalAmount = Math.max(0, finalPrice - (appliedDiscountAmount || 0));
                document.getElementById('choiceFullAmount').innerText = finalAmount.toLocaleString('vi-VN') + 'đ';
                document.getElementById('choiceDepositAmount').innerText = (finalAmount / 2).toLocaleString('vi-VN') + 'đ';

                openModal('paymentChoiceModal');
            } else {
                alert(data.message);
            }
        })
        .catch(err => alert('Lỗi kết nối máy chủ!'));
}

function proceedToPayment(type) {
    currentPaymentType = type; // Lưu lại lựa chọn ('Coc50' hoặc 'Full')
    closeModal('paymentChoiceModal');
    updatePaymentModal();
    openModal('paymentModal'); // Chuyển sang màn hình quét QR
}

function updatePaymentModal() {
    const voucherInfo = document.getElementById('paymentVoucherInfo');
    const voucherCodeDisplay = document.getElementById('paymentVoucherCode');
    const voucherDiscountDisplay = document.getElementById('paymentVoucherDiscount');
    const finalAmountDisplay = document.getElementById('paymentFinalAmount');
    const amountLabel = finalAmountDisplay.previousElementSibling; // Thẻ <p> chứa chữ "Số tiền cần chuyển:"

    if (appliedVoucherCode && appliedDiscountAmount > 0) {
        voucherInfo.style.display = 'block';
        voucherCodeDisplay.innerText = appliedVoucherCode;
        voucherDiscountDisplay.innerText = appliedDiscountAmount.toLocaleString('vi-VN') + 'đ';
    } else {
        voucherInfo.style.display = 'none';
    }

    const finalAmount = Math.max(0, finalPrice - (appliedDiscountAmount || 0));

    // Tùy chỉnh text và số tiền dựa trên lựa chọn
    if (currentPaymentType === 'Coc50') {
        amountLabel.innerText = "Số tiền cần chuyển (Cọc 50%):";
        finalAmountDisplay.innerText = (finalAmount / 2).toLocaleString('vi-VN') + 'đ';
    } else {
        amountLabel.innerText = "Số tiền cần chuyển (Thanh toán 100%):";
        finalAmountDisplay.innerText = finalAmount.toLocaleString('vi-VN') + 'đ';
    }
}

// Thêm hàm này vào customer-booking.js
async function closePaymentModal() {
    if (currentBookingIds.length > 0) {
        const confirmExit = confirm("Bạn chưa hoàn tất thanh toán! Tắt cửa sổ này sẽ hủy đơn đặt sân của bạn. Bạn có chắc chắn muốn thoát?");
        if (!confirmExit) return;

        await fetch('/San/CancelPendingBookings', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(currentBookingIds)
        });

        currentBookingIds = [];
        resetVoucherState();
        await fetchBookedSlots();
    }
    closeModal('paymentModal');
    closeModal('paymentChoiceModal'); // Thêm dòng này
}

// ================= LOGIC THANH TOÁN & LỊCH SỬ =================
function confirmPayment() {
    if (currentBookingIds.length === 0) return;

    fetch('/San/ConfirmPayment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            bookingIds: currentBookingIds,
            voucherCode: appliedVoucherCode || null,
            paymentType: currentPaymentType // Gửi lựa chọn lên C#
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

// ================= LOGIC THANH TOÁN & LỊCH SỬ =================
function loadHistory() {
    if (!window.isLoggedIn) { alert("Vui lòng đăng nhập!"); return; }

    fetch('/San/GetBookingHistory', { cache: 'no-store' })
        .then(res => res.json())
        .then(data => {
            // Lưu dữ liệu gốc vào biến toàn cục
            userHistoryData = data;

            // Xóa bộ lọc và in ra toàn bộ bảng
            resetHistoryFilters();

            openModal('historyModal');
        });
}

function renderHistoryTable(dataToRender) {
    const tbody = document.getElementById('historyTableBody');
    if (!tbody) return;
    tbody.innerHTML = '';

    // Nếu lọc không ra kết quả
    if (dataToRender.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align: center; padding: 25px; color: #666;">Không tìm thấy đơn đặt sân nào phù hợp.</td></tr>`;
        return;
    }

    dataToRender.forEach((item) => {
        // CẦN THIẾT: Tìm lại index gốc của item trong userHistoryData 
        // để khi bấm nút Đánh Giá / Hóa Đơn không bị gọi sai sân
        const originalIndex = userHistoryData.indexOf(item);
        let actionHtml = '';

        if (['Đã thanh toán', 'Hoàn thành', 'Đã xác nhận', 'Đang chơi'].includes(item.trangThai)) {
            actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto;" onclick="viewCustomerInvoice(${originalIndex})">
                             <i class="fas fa-file-invoice"></i> Hóa đơn
                           </button>`;
        }

        // --- THÊM NÚT HỦY HOẶC ĐANG YÊU CẦU HỦY ---
        if (['Chờ xác nhận', 'Đã xác nhận'].includes(item.trangThai)) {
            if (item.yeuCauHuy) {
                actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto; margin-left: 5px; background: #6c757d; border: none;" onclick="openCancelRequestModal(${originalIndex}, true)">
                                 <i class="fas fa-spinner fa-spin"></i> Đang chờ hủy
                               </button>`;
            } else {
                actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto; margin-left: 5px; background: #dc3545; border: none;" onclick="openCancelRequestModal(${originalIndex}, false)">
                                 <i class="fas fa-times"></i> Hủy
                               </button>`;
            }
        }
        if (['Chờ xác nhận', 'Đã xác nhận'].includes(item.trangThai)) {
            if (item.yeuCauSua) {
                actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto; margin-left: 5px; background: #fd7e14; border: none;"><i class="fas fa-spinner fa-spin"></i> Chờ duyệt sửa</button>`;
            } else {
                actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto; margin-left: 5px; background: #0d6efd; border: none;" onclick="openEditRequestModal(${originalIndex})"><i class="fas fa-edit"></i> Sửa</button>`;
            }
        }

        if (item.trangThai === 'Hoàn thành') {
            if (item.daDanhGia) {
                actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto; margin-left: 5px; background: #17a2b8; border: none;" onclick="openReviewModal(${originalIndex})">
                                 <i class="fas fa-eye"></i> Xem đánh giá
                               </button>`;
            } else {
                actionHtml += `<button class="btn-primary-blue" style="padding: 6px 10px; font-size: 12px; width: auto; margin-left: 5px; background: #f59f00; border: none;" onclick="openReviewModal(${originalIndex})">
                                 <i class="fas fa-star"></i> Đánh giá
                               </button>`;
            }
        }

        if (!actionHtml) actionHtml = '-';

        // Xử lý màu sắc hiển thị riêng cho các trạng thái đặc biệt
        let statusHtml = `<span class="status-paid">${item.trangThai}</span>`;
        if (item.trangThai === 'Đã hoàn tiền') {
            statusHtml = `<span style="color: #28a745; font-weight: bold; background: #e6f4ea; padding: 4px 8px; border-radius: 4px;"></i> Đã hoàn tiền</span>`;
        } else if (item.trangThai === 'Đã hủy') {
            statusHtml = `<span style="color: #dc3545; font-weight: bold; background: #fde8e8; padding: 4px 8px; border-radius: 4px;">Đã hủy</span>`;
        }

        tbody.innerHTML += `
            <tr>
                <td>${item.maHoaDon}</td>
                <td>${item.ngayThanhToan}</td>
                <td>${item.loaiSan}</td>
                <td>${item.khungGio}</td>
                <td>${item.tongTien.toLocaleString('vi-VN')}đ</td>
                <td>${statusHtml}</td>
                <td style="text-align: center;">${actionHtml}</td>
            </tr>`;
    });
}

function filterHistory() {
    const dateFilter = document.getElementById('histFilterDate').value;
    const startFilter = document.getElementById('histFilterStartTime').value;
    const endFilter = document.getElementById('histFilterEndTime').value;
    const typeFilter = document.getElementById('histFilterType').value;

    // RÀNG BUỘC 1: Phải chọn đủ 2 ô giờ
    if ((startFilter && !endFilter) || (!startFilter && endFilter)) {
        alert("Vui lòng chọn đầy đủ cả 'Giờ bắt đầu' và 'Giờ kết thúc' để lọc theo khung giờ!");
        return; // Dừng hệ thống, không lọc nữa
    }

    // RÀNG BUỘC 2: Giờ kết thúc phải lớn hơn giờ bắt đầu
    if (startFilter && endFilter) {
        if (parseInt(startFilter) >= parseInt(endFilter)) {
            alert("Lỗi: Giờ kết thúc phải lớn hơn Giờ bắt đầu!");
            return;
        }
    }

    let filtered = userHistoryData;

    // Lọc theo ngày chơi
    if (dateFilter) {
        filtered = filtered.filter(x => x.ngayChoi === dateFilter);
    }

    // Lọc theo loại sân
    if (typeFilter) {
        filtered = filtered.filter(x => x.loaiSan === typeFilter);
    }

    // Lọc theo giờ (Tìm các đơn nằm TRONG khoảng thời gian đã chọn)
    if (startFilter && endFilter) {
        const fStart = parseInt(startFilter);
        const fEnd = parseInt(endFilter);

        filtered = filtered.filter(x => {
            if (!x.khungGioGoc) return false;

            // Chuyển chuỗi "14,15,16" thành mảng số để so sánh
            const hours = x.khungGioGoc.split(',').map(Number).sort((a, b) => a - b);
            if (hours.length === 0) return false;

            const startHour = hours[0];
            const endHour = hours[hours.length - 1] + 1; // Khung 16 nghĩa là 16:00-17:00, nên kết thúc là 17

            // Điều kiện: Đơn đặt sân phải nằm hoàn toàn trong khoảng giờ người dùng chọn
            // Ví dụ: Chọn lọc từ 05:00 đến 12:00 -> Đơn 07:00 đến 09:00 sẽ hợp lệ (7 >= 5 và 9 <= 12)
            return startHour >= fStart && endHour <= fEnd;
        });
    }

    renderHistoryTable(filtered);
}

function resetHistoryFilters() {
    document.getElementById('histFilterDate').value = '';
    document.getElementById('histFilterStartTime').value = '';
    document.getElementById('histFilterEndTime').value = '';
    document.getElementById('histFilterType').value = '';

    // In lại toàn bộ dữ liệu gốc
    renderHistoryTable(userHistoryData);
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

    // Lấy ngày hiện tại để kiểm tra giờ quá khứ
    const today = new Date();
    const todayStr = today.getFullYear() + '-' + String(today.getMonth() + 1).padStart(2, '0') + '-' + String(today.getDate()).padStart(2, '0');
    const isToday = date === todayStr;
    const currentHour = today.getHours();

    for (let i = 5; i < 24; i++) {
        const slot = document.createElement('div');
        slot.className = 'time-slot';

        // Điều kiện kiểm tra xem có phải là giờ đã qua trong ngày hôm nay không
        const isPastHour = isToday && (i <= currentHour);

        if (dtBookedSlots.includes(i)) {
            slot.classList.add('slot-busy');
            slot.innerHTML = `${i}:00 - ${i + 1}:00 <br> (Bận)`;
        } else if (isPastHour) {
            // Hiển thị giờ đã qua (dùng chung class disabled với popup đặt sân)
            slot.classList.add('disabled');
            slot.innerHTML = `${i}:00 - ${i + 1}:00 <br> (Đã qua)`;
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
// ================= LOGIC HÓA ĐƠN & XUẤT PDF =================
function viewCustomerInvoice(index) {
    const item = userHistoryData[index];
    if (!item) return;

    // Gắn dữ liệu đầy đủ vào Modal
    document.getElementById('cusInvId').innerText = item.maHoaDon;
    document.getElementById('cusInvCustomer').innerText = item.khachHang;
    document.getElementById('cusInvPhone').innerText = item.soDienThoai;
    document.getElementById('cusInvCourtName').innerText = item.tenSan;
    document.getElementById('cusInvCourtType').innerText = item.loaiSan;
    document.getElementById('cusInvPlayDate').innerText = item.ngayChoiDisplay;
    document.getElementById('cusInvTime').innerText = item.khungGio;
    document.getElementById('cusInvDate').innerText = item.ngayThanhToan;
    document.getElementById('cusInvMethod').innerText = item.phuongThucThanhToan;

    // Tính toán Tạm tính và Giảm giá
    const finalAmount = item.tongTien;
    const discount = item.soTienGiam || 0;
    const subTotal = finalAmount + discount;

    document.getElementById('cusInvTotal').innerText = subTotal.toLocaleString('vi-VN') + 'đ';
    document.getElementById('cusInvDiscount').innerText = '-' + discount.toLocaleString('vi-VN') + 'đ';
    document.getElementById('cusInvFinal').innerText = finalAmount.toLocaleString('vi-VN') + 'đ';

    // Gắn sự kiện in PDF cho nút "Tải PDF"
    const btnPrint = document.getElementById('cusBtnPrintPdf');
    btnPrint.onclick = function (e) {
        e.preventDefault();
        downloadInvoicePDF(item.maHoaDon);
    };

    // Chuyển đổi Modal
    closeModal('historyModal');
    openModal('customerInvoiceModal');
}

function downloadInvoicePDF(invoiceId) {
    const element = document.getElementById('invoicePrintArea');

    // Cấu hình thư viện html2pdf
    const opt = {
        margin: [15, 15, 15, 15],
        filename: `HoaDon_PickleballSmash_${invoiceId}.pdf`,
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: { scale: 2, useCORS: true },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
    };

    if (typeof html2pdf === 'undefined') {
        alert("Đang tải thư viện tạo PDF, vui lòng thử lại sau vài giây.");
        return;
    }

    // Hiệu ứng UX: Đổi text nút trong lúc tải
    const btnPrint = document.getElementById('cusBtnPrintPdf');
    const originalText = btnPrint.innerHTML;
    btnPrint.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang tạo PDF...';
    btnPrint.style.pointerEvents = 'none';

    // Tạo và tải PDF kèm theo bắt lỗi (catch)
    html2pdf().set(opt).from(element).save()
        .then(() => {
            // Khi thành công hoặc người dùng đã đóng hộp thoại save
            btnPrint.innerHTML = originalText;
            btnPrint.style.pointerEvents = 'auto';
        })
        .catch(err => {
            // Khi có lỗi hoặc luồng bị gián đoạn
            console.error("Lỗi xuất PDF:", err);
            btnPrint.innerHTML = originalText;
            btnPrint.style.pointerEvents = 'auto';
            alert("Quá trình xuất PDF bị gián đoạn. Vui lòng thử lại!");
        });
}
// ================= LOGIC ĐÁNH GIÁ SÂN =================
let currentReviewSanId = null;
let currentReviewDonDatSanId = null;

function openReviewModal(index) {
    const item = userHistoryData[index];
    if (!item) return;

    currentReviewSanId = item.sanId;
    currentReviewDonDatSanId = item.donDatSanId;

    const commentBox = document.getElementById('reviewComment');
    const submitBtn = document.getElementById('btnSubmitReview');
    const starContainer = document.getElementById('starContainer');

    if (item.daDanhGia) {
        // CHẾ ĐỘ CHỈ XEM (READ-ONLY)
        commentBox.value = item.binhLuan || 'Không có bình luận.';
        commentBox.readOnly = true;
        commentBox.style.backgroundColor = '#f4f6f8'; // Làm xám ô text

        const starInput = document.getElementById('star' + item.soSao);
        if (starInput) starInput.checked = true;

        starContainer.style.pointerEvents = 'none'; // Khóa click chọn sao
        submitBtn.style.display = 'none'; // Ẩn nút gửi
    } else {
        // CHẾ ĐỘ VIẾT ĐÁNH GIÁ MỚI
        commentBox.value = '';
        commentBox.readOnly = false;
        commentBox.style.backgroundColor = '#e1e1e1';

        document.getElementById('star5').checked = true; // Mặc định 5 sao

        starContainer.style.pointerEvents = 'auto'; // Cho phép click
        submitBtn.style.display = 'block'; // Hiện nút gửi
    }

    closeModal('historyModal');
    openModal('reviewModal');
}

function submitReview() {
    const comment = document.getElementById('reviewComment').value.trim();
    const stars = document.querySelector('.star-rating input:checked');
    const starValue = stars ? stars.value : 5;

    fetch('/San/SubmitReview', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            SanID: currentReviewSanId,
            DonDatSanID: currentReviewDonDatSanId,
            SoSao: parseInt(starValue),
            BinhLuan: comment
        })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                alert("Cảm ơn bạn đã gửi đánh giá!");
                closeModal('reviewModal');
                loadHistory();
                openModal('historyModal');
            } else {
                alert(data.message || "Có lỗi xảy ra, vui lòng thử lại.");
            }
        })
        .catch(() => alert("Không thể kết nối đến máy chủ!"));
}
// ================= LOGIC XEM DANH SÁCH ĐÁNH GIÁ =================
let currentCourtReviewsData = [];

function openCourtReviewsModal() {
    if (!currentDetailCourtId) return;

    fetch(`/San/GetCourtReviews?sanId=${currentDetailCourtId}`)
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                currentCourtReviewsData = data.data;

                // Cập nhật thống kê Header
                document.getElementById('rvTotalAvg').innerText = data.stats.avg.toFixed(1);
                document.getElementById('rvFilterAll').innerText = `Tất cả (${data.stats.total})`;
                document.getElementById('rvFilter5').innerText = `5 Sao (${data.stats.counts.s5})`;
                document.getElementById('rvFilter4').innerText = `4 Sao (${data.stats.counts.s4})`;
                document.getElementById('rvFilter3').innerText = `3 Sao (${data.stats.counts.s3})`;
                document.getElementById('rvFilter2').innerText = `2 Sao (${data.stats.counts.s2})`;
                document.getElementById('rvFilter1').innerText = `1 Sao (${data.stats.counts.s1})`;

                // Mặc định chọn nút "Tất cả"
                const filterBtns = document.querySelectorAll('.btn-filter-star');
                filterBtns.forEach(b => b.classList.remove('active'));
                document.getElementById('rvFilterAll').classList.add('active');

                // Render danh sách
                renderReviewListHTML(currentCourtReviewsData);

                closeModal('detailModal');
                openModal('courtReviewsModal');
            } else {
                alert("Không thể lấy danh sách đánh giá lúc này.");
            }
        })
        .catch(() => alert("Lỗi kết nối đến máy chủ!"));
}

function filterReviewsByStar(star, btnElement) {
    // Đổi màu nút được click
    document.querySelectorAll('.btn-filter-star').forEach(b => b.classList.remove('active'));
    btnElement.classList.add('active');

    // Lọc mảng
    if (star === 0) {
        renderReviewListHTML(currentCourtReviewsData);
    } else {
        const filtered = currentCourtReviewsData.filter(r => r.soSao === star);
        renderReviewListHTML(filtered);
    }
}

function renderReviewListHTML(reviews) {
    const container = document.getElementById('courtReviewsList');
    container.innerHTML = '';

    if (reviews.length === 0) {
        container.innerHTML = '<div style="text-align:center; padding: 40px; color:#888;">Chưa có đánh giá nào.</div>';
        return;
    }

    reviews.forEach(r => {
        // Tạo HTML cho số sao
        let starsHtml = '';
        for (let i = 1; i <= 5; i++) {
            starsHtml += i <= r.soSao ? '<i class="fas fa-star"></i>' : '<i class="far fa-star" style="color: #ddd;"></i>';
        }

        // Ẩn bớt ký tự tên người dùng (Giống Shopee: Ng****nh)
        let maskedName = r.hoTen;
        if (maskedName.length > 2 && maskedName !== "Khách hàng ẩn danh") {
            maskedName = maskedName.substring(0, 2) + "****" + maskedName.substring(maskedName.length - 2);
        }

        container.innerHTML += `
            <div class="review-item">
                <div class="review-avatar"><i class="fas fa-user"></i></div>
                <div class="review-content">
                    <div class="review-author">${maskedName}</div>
                    <div class="review-stars">${starsHtml}</div>
                    <div class="review-date">${r.ngayDanhGia}</div>
                    <div class="review-text">${r.binhLuan ? r.binhLuan.replace(/\\n/g, '<br>') : ''}</div>
                </div>
            </div>
        `;
    });
}

// ================= LOGIC YÊU CẦU SỬA =================
function openEditRequestModal(index) {
    const item = userHistoryData[index];
    if (!item) return;

    // Bật cờ Edit Mode và lưu ID đơn cần sửa
    isEditMode = true;
    editTargetBookingId = item.donDatSanId;

    // Đổ dữ liệu cũ vào Popup Đặt sân
    const sanSelect = document.getElementById('bkSan');
    if (sanSelect) sanSelect.value = item.sanId;

    const dateInput = document.getElementById('bkDate');
    if (dateInput) dateInput.value = item.ngayChoi;

    // Xử lý khung giờ cũ
    if (item.khungGioGoc) {
        selectedSlots = item.khungGioGoc.split(',').map(Number);
    } else {
        selectedSlots = [];
    }

    // Đổi Text giao diện
    const modalTitle = document.querySelector('#bookingModal .auth-title');
    if (modalTitle) modalTitle.innerText = "Yêu cầu chỉnh sửa đơn";

    const btnSubmit = document.getElementById('btnSubmitBookingMain');
    if (btnSubmit) btnSubmit.innerText = "Gửi Yêu Cầu Sửa";

    const btnBack = document.getElementById('btnBackToHistory');
    if (btnBack) btnBack.style.display = 'block';

    closeModal('historyModal');
    openModal('bookingModal');

    // Gọi tải lại lưới giờ để check xem khung giờ cũ có bị trùng không
    fetchBookedSlots();
}

// Xử lý nút Reset khi tắt Popup Sửa (để các lần bấm Đặt Sân tiếp theo không bị dính cờ Edit)
function closeBookingModal() {
    closeModal('bookingModal');
    isEditMode = false;
    editTargetBookingId = 0;

    const modalTitle = document.querySelector('#bookingModal .auth-title');
    if (modalTitle) modalTitle.innerText = "Đặt sân";

    const btnSubmit = document.getElementById('btnSubmitBookingMain');
    if (btnSubmit) btnSubmit.innerText = "Đặt Sân";

    const btnBack = document.getElementById('btnBackToHistory');
    if (btnBack) btnBack.style.display = 'none';

    // Nếu khách đang xem lịch sử thì trả khách về Lịch sử
    if (userHistoryData.length > 0) {
        openModal('historyModal');
    }
}
// ================= LOGIC YÊU CẦU HỦY =================
let currentCancelBookingId = 0;

function openCancelRequestModal(index, isRevokeMode) {
    const item = userHistoryData[index];
    if (!item) return;
    currentCancelBookingId = item.donDatSanId;

    // Tính toán thời gian chênh lệch (Cảnh báo 60 phút)
    let timeDiffMsg = "";
    if (!isRevokeMode && item.ngayChoi && item.khungGioGoc) {
        try {
            const dateParts = item.ngayChoi.split('-'); // Lấy mảng [yyyy, MM, dd]
            const hours = item.khungGioGoc.split(',').map(Number).sort((a, b) => a - b);

            if (dateParts.length === 3 && hours.length > 0) {
                const firstHour = hours[0];
                // Tạo đối tượng Date cho giờ nhận sân (Tháng trong JS bắt đầu từ 0 nên phải -1)
                const playDate = new Date(dateParts[0], dateParts[1] - 1, dateParts[2], firstHour, 0, 0);
                const now = new Date();

                // Tính số phút chênh lệch
                const diffMinutes = (playDate - now) / (1000 * 60);

                if (diffMinutes < 60) {
                    timeDiffMsg = "<br><br><span style='color: #dc3545; font-weight: bold; padding: 10px; background: #fde8e8; border-radius: 5px; display: inline-block;'><i class='fas fa-exclamation-triangle'></i> CẢNH BÁO: Hiện tại cách giờ chơi chưa tới 60 phút. Nếu bạn hủy đơn lúc này, bạn sẽ KHÔNG ĐƯỢC HOÀN TIỀN.</span>";
                } else {
                    timeDiffMsg = "<br><br><span style='color: #28a745; font-weight: bold;'><i class='fas fa-info-circle'></i> Hủy đơn hợp lệ (trước 60 phút): Bạn sẽ được hoàn lại tiền (nếu có).</span>";
                }
            }
        } catch (e) { console.error("Lỗi tính thời gian", e); }
    }

    if (isRevokeMode) {
        document.getElementById('crTitle').innerText = "Đã gửi yêu cầu hủy";
        document.getElementById('crMessage').innerHTML = "Yêu cầu của bạn đang được nhân viên xử lý. Bạn có muốn thu hồi lại yêu cầu này không?";
        document.getElementById('crBtnGroupNormal').style.display = 'none';
        document.getElementById('crBtnGroupRevoke').style.display = 'flex';
    } else {
        document.getElementById('crTitle').innerText = "Xác nhận hủy đơn";
        document.getElementById('crMessage').innerHTML = "Bạn có chắc chắn muốn gửi yêu cầu hủy đơn đặt sân này đến nhân viên không?" + timeDiffMsg;
        document.getElementById('crBtnGroupNormal').style.display = 'flex';
        document.getElementById('crBtnGroupRevoke').style.display = 'none';
    }

    closeModal('historyModal');
    openModal('cancelRequestModal');
}

function submitCancelRequest(isRequesting) {
    fetch('/San/ToggleCancelRequest', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ DonDatSanID: currentCancelBookingId, IsRequesting: isRequesting })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                closeModal('cancelRequestModal');
                loadHistory(); // Tự động tải lại bảng lịch sử để cập nhật trạng thái
            } else {
                alert(data.message);
            }
        })
        .catch(err => alert("Lỗi kết nối đến máy chủ. Vui lòng thử lại!"));
}