# 🚀 BẮT ĐẦU TẠI ĐÂY - HƯỚNG DẪN QUY TẮC CỐ ĐỊNH

> **Chào mừng đến với Quy tắc Lập trình Bizcore ERP!**  
> Tài liệu này sẽ hướng dẫn bạn đến tài nguyên phù hợp dựa trên vai trò của bạn.

---

## 👤 Vai Trò Của Bạn Là Gì?

### 👨‍💻 **Lập trình viên Mới**

**Thời gian đọc**: 30 phút  
**Mục tiêu**: Hiểu các quy tắc lập trình trước khi viết code

1. **Trước tiên (5 phút)**:
   - Đọc: [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md)
   - Tập trung vào: "5 Quy Tắc Quan Trọng Nhất"

2. **Tiếp theo (10 phút)**:
   - Đọc: [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) các phần:
     - Phần 2: Quy Tắc Đặt Tên
     - Phần 3: Cấu Trúc Dự Án
     - Phần 4: Clean Code & Kiến Trúc

3. **Tham chiếu khi cần**:
   - Các phần 5-9 cho các chủ đề cụ thể
   - Sao chép template code từ [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md)

4. **Cài đặt (5 phút)**:
   - IDE sẽ tự động tải `.editorconfig`
   - Kiểm tra: Lưu file C#, xem định dạng tự động

---

### 🔍 **Người Đánh Giá Code**

**Thời gian đọc**: 45 phút  
**Mục tiêu**: Đánh giá PR sử dụng các tiêu chuẩn nhất quán

1. **Đánh dấu trang**:
   - [`CODE_REVIEW_GUIDE.md`](docs/CODE_REVIEW_GUIDE.md) - Tài liệu tham khảo chính

2. **Đọc** (30 phút):
   - Phần 1-5: Danh sách kiểm tra (kiến trúc, đặt tên, async, ngoại lệ, phân quyền)
   - Phần 6-10: Hướng dẫn chất lượng và kiểm thử

3. **Sử dụng** (trên mỗi PR):
   - Phần 11: Quy trình Đánh giá (từng bước)
   - Sử dụng danh sách kiểm tra ở đầu mỗi phần
   - Tham chiếu các ví dụ code

4. **Tham khảo** (khi cần):
   - [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md) để tìm kiếm nhanh
   - [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) để giải thích chi tiết

---

### 👔 **Người Quản Lý Dự Án / Kiến Trúc Sư**

**Thời gian đọc**: 2 giờ  
**Mục tiêu**: Triển khai quy tắc trên toàn dự án

1. **Đọc tất cả** (theo thứ tự):
   - [`CONVENTIONS_README.md`](docs/CONVENTIONS_README.md) - Tổng quan (10 phút)
   - [`IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) - Kế hoạch triển khai (20 phút)
   - [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) - Tài liệu đầy đủ (40 phút)
   - [`CODE_REVIEW_GUIDE.md`](docs/CODE_REVIEW_GUIDE.md) - Tiêu chuẩn đánh giá (30 phút)
   - [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md) - Tham khảo nhanh (10 phút)

2. **Cài đặt**:
   - Kiểm tra `.editorconfig` ở thư mục gốc của kho
   - Cấu hình StyleCop trong dự án
   - Thêm vào đường dẫn CI/CD

3. **Lên kế hoạch**:
   - Sử dụng lịch trình triển khai 4 tuần từ [`IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md)
   - Lên lịch đào tạo nhóm
   - Chuẩn bị đánh giá PR đầu tiên

---

### 🎯 **Tìm Kiếm Điều Gì Đó Cụ Thể?**

| Tôi cần... | Đọc cái này |
| ----------- | ----------- |
| **Trả lời nhanh** | [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md) |
| **Cách đặt tên** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 2 |
| **Quy tắc kiến trúc** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 3-4 |
| **Xử lý ngoại lệ** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 5 |
| **Tiêu chuẩn ghi log** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 6 |
| **Mô hình Async** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 7 |
| **Quy tắc bảo mật** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 8 |
| **Mô hình cơ sở dữ liệu** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 9 |
| **Tiêu chuẩn kiểm thử** | [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md) Phần 10 |
| **Danh sách kiểm tra đánh giá** | [`CODE_REVIEW_GUIDE.md`](docs/CODE_REVIEW_GUIDE.md) Phần 1-10 |
| **Kế hoạch triển khai nhóm** | [`IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) |
| **Tổng quan & điều hướng** | [`CONVENTIONS_README.md`](docs/CONVENTIONS_README.md) |
| **Template code** | [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md) "Code Templates" |

---

## ⚡ 5 QUY TẮC QUAN TRỌNG NHẤT

**Những quy tắc này là KHÔNG THỂ THƯƠNG LƯỢNG. Kiểm tra trong mọi PR.**

### 1️⃣ **KHÔNG ĐƯA LOGIC KINH DOANH VÀO CONTROLLERS**

```csharp
❌ SAI:
[HttpPost]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req)
{
    if (req.Amount > 1_000_000) return BadRequest();
    var invoice = new Invoice { Amount = req.Amount };
    await _context.SaveChangesAsync();
    return Ok(invoice);
}

✅ ĐÚNG:
[HttpPost]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req)
{
    var invoice = Invoice.Create(req.CustomerName, req.Amount);
    var created = await _invoiceService.CreateAsync(invoice);
    return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
}
```

### 2️⃣ **LUÔN THÊM PHÂN QUYỀN**

```csharp
❌ SAI:
[HttpPost]
public async Task<IActionResult> CreateInvoice() { }

✅ ĐÚNG:
[HttpPost]
[Authorize(Policy = Permissions.Invoice.Create)]
public async Task<IActionResult> CreateInvoice() { }
```

### 3️⃣ **DÙNG SỰ KIỆN CHO LIÊN LẠC GIỮA CÁC DỊCH VỤ**

```csharp
❌ SAI:
var payment = await _httpClient.GetAsync($"http://payment/api/{id}");

✅ ĐÚNG:
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        await _invoiceService.MarkAsPaidAsync(context.Message.InvoiceId);
    }
}
```

### 4️⃣ **VHI CÁC NGOẠI LỆ CÓ LOẠI**

```csharp
❌ SAI:
if (string.IsNullOrEmpty(invoice.CustomerName)) return -1;

✅ ĐÚNG:
if (string.IsNullOrEmpty(invoice.CustomerName))
    throw new DomainException("Tên khách hàng là bắt buộc");
```

### 5️⃣ **DÙNG ASYNC/AWAIT Ở KHẮP NƠI**

```csharp
❌ SAI:
public Invoice GetById(Guid id) 
    => _context.Invoices.FirstOrDefault(i => i.Id == id);

✅ ĐÚNG:
public async Task<Invoice?> GetByIdAsync(Guid id) 
    => await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
```

---

## 📚 Hướng Dẫn Tài Liệu

| Tài Liệu | Mục Đích | Phù Hợp Cho |
| ---------- | ---------- | ---------- |
| **CONVENTIONS_README.md** | Điều hướng & tổng quan | Tất cả người dùng |
| **CONVENTIONS_QUICK_REFERENCE.md** | Tìm kiếm nhanh, template | Trong quá trình phát triển |
| **CODING_CONVENTIONS.md** | Tài liệu tham khảo hoàn chỉnh | Hướng dẫn chi tiết |
| **CODE_REVIEW_GUIDE.md** | Tiêu chuẩn đánh giá | Những người đánh giá code |
| **IMPLEMENTATION_GUIDE.md** | Kế hoạch triển khai | Người quản lý dự án |

---

## 🛠 Danh Sách Kiểm Tra Cài Đặt

- [ ] Đọc các phần phù hợp ở trên ✅
- [ ] `.editorconfig` nằm trong thư mục gốc của dự án (tự động tải bởi IDE)
- [ ] IDE hiển thị gợi ý định dạng
- [ ] Thử lưu file C#, xem định dạng tự động
- [ ] Đọc qua các template code trong tài liệu tham khảo nhanh
- [ ] Sẵn sàng viết code! 🚀

---

## 💡 Mẹo Hữu Ích

1. **Khi không chắc chắn**: Kiểm tra [`CONVENTIONS_QUICK_REFERENCE.md`](docs/CONVENTIONS_QUICK_REFERENCE.md) trước tiên
2. **Cần ví dụ**: Tìm kiếm trong [`CODING_CONVENTIONS.md`](docs/CODING_CONVENTIONS.md)
3. **Đánh giá PR**: Sử dụng danh sách kiểm tra từ [`CODE_REVIEW_GUIDE.md`](docs/CODE_REVIEW_GUIDE.md)
4. **Đào tạo nhóm**: Theo dõi [`IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md)
5. **Câu hỏi?**: Hỏi trong cuộc họp nhóm hoặc kênh #architecture

---

## ✅ Bạn Đã Sẵn Sàng

- ✅ Tài liệu đã sẵn sàng sử dụng
- ✅ .editorconfig tự động áp dụng tiêu chuẩn  
- ✅ Danh sách kiểm tra đánh giá ngăn chặn vấn đề
- ✅ Template sẵn sàng để sao chép
- ✅ Tài liệu tham khảo hoàn chỉnh cho bất kỳ câu hỏi nào

> **Bắt đầu viết code! Tham khảo tài liệu khi cần. Chúc lập trình vui vẻ! 🚀**

---

**Vị trí của tất cả tài liệu**: Thư mục `/docs/` + `.editorconfig` ở thư mục gốc

**Có câu hỏi?** Xem phần FAQ tại [`CONVENTIONS_README.md`](docs/CONVENTIONS_README.md)
