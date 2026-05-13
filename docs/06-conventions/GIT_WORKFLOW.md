# 🌿 GIT WORKFLOW & COLLABORATION - BIZCORE ERP

> **Mục đích**: Quy định các tiêu chuẩn về quản lý mã nguồn, quy trình làm việc nhóm và kiểm soát chất lượng qua Pull Requests.

---

## 🏗️ 1. Mô hình Nhánh (Branching Strategy)

Hệ thống áp dụng mô hình **Git Flow rút gọn** để đảm bảo tính ổn định của mã nguồn.

### Các nhánh chính

- **`main`**: Nhánh chứa mã nguồn đang chạy trên môi trường Production. Tuyệt đối không commit trực tiếp.
- **`develop`**: Nhánh tích hợp chính. Các tính năng mới sẽ được merge vào đây trước khi lên release.

### Các nhánh tạm thời

- **`feature/BC-{ID}-{description}`**: Phát triển tính năng mới.
- **`bugfix/BC-{ID}-{description}`**: Sửa lỗi từ nhánh develop.
- **`release/v{version}`**: Chuẩn bị cho việc deploy lên production.
- **`hotfix/BC-{ID}-{description}`**: Sửa lỗi khẩn cấp trực tiếp trên main.

> [!NOTE]
> **BC** là viết tắt của **BizCore**. Tiền tố này giúp định danh dự án và thường liên kết trực tiếp với mã số Task/Issue (ID) trên các hệ thống quản lý công việc.
> **Ví dụ**: `feature/BC-123-add-invoice-approval`

---

## 📝 2. Quy chuẩn Commit Message

Bizcore ERP sử dụng chuẩn **Conventional Commits** để tự động hóa việc tạo changelog.

**Cấu trúc:**
`type(scope): description`

**Các loại (Type):**

- **`feat`**: Một tính năng mới.
- **`fix`**: Sửa một lỗi.
- **`docs`**: Thay đổi về tài liệu.
- **`style`**: Thay đổi về format, semicolon... (không ảnh hưởng logic).
- **`refactor`**: Thay đổi code nhưng không sửa lỗi hay thêm tính năng.
- **`perf`**: Cải thiện hiệu năng.
- **`test`**: Thêm hoặc sửa các bài unit test.
- **`chore`**: Các thay đổi về build tool, library dependency...

**Ví dụ:**

- `feat(invoice): thêm quy trình duyệt hóa đơn tự động`
- `fix(payment): sửa lỗi tính sai số dư khi hoàn tiền`
- `docs(readme): cập nhật hướng dẫn cài đặt docker`

---

## 🔄 3. Quy trình Pull Request (PR)

Tất cả các thay đổi mã nguồn phải đi qua PR để được review.

### Các bước thực hiện

1. **Cập nhật code mới nhất**: `git pull origin develop` vào nhánh của bạn.
2. **Đẩy code lên remote**: `git push origin feature/...`.
3. **Tạo PR**: Chọn target branch là `develop`.
4. **Gán Reviewer**: Ít nhất một thành viên khác phải review code của bạn.
5. **Vượt qua CI**: Đảm bảo tất cả các bài test tự động đều pass.
6. **Merge**: Sử dụng **Squash and Merge** để giữ lịch sử commit trên `develop` luôn sạch sẽ.

### Tiêu chí Merge

- ✅ Ít nhất 1 Approve từ Reviewer.
- ✅ Không có xung đột (Conflict).
- ✅ Tất cả các comment review đã được giải quyết hoặc phản hồi.
- ✅ Pass CI/CD checks.

---

## 🧹 4. Các hoạt động sau khi Merge

1. **Xóa nhánh**: Xóa nhánh feature trên cả local và remote sau khi đã merge thành công.
2. **Cập nhật Task**: Chuyển trạng thái task trên Github/Jira/Trello sang `Done`.
3. **Thông báo**: Thông báo cho team nếu có thay đổi quan trọng về cấu hình (appsettings, môi trường...).

---

## 📏 5. Quy tắc đặt tên trong Code

Mặc dù đã có chi tiết trong [Coding Conventions](CODING_CONVENTIONS.md), dưới đây là các quy tắc cốt lõi:

- **Class/Method**: `PascalCase` (ví dụ: `CreateInvoiceAsync`).
- **Interface**: Bắt đầu bằng chữ `I` (ví dụ: `IInvoiceService`).
- **Variable/Parameter**: `camelCase` (ví dụ: `totalAmount`).
- **Private Field**: `_camelCase` (ví dụ: `_invoiceRepository`).
- **Async Method**: Luôn có hậu tố `Async` (ví dụ: `ProcessPaymentAsync`).

---

> **Tài liệu liên quan**:
>
> - [Code Review Guide](CODE_REVIEW_GUIDE.md)
> - [Coding Conventions](CODING_CONVENTIONS.md)
