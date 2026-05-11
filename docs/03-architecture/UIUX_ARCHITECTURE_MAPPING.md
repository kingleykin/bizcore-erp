# Phân bổ UI/UX Design cho Kiến trúc Microservices Mới

Tài liệu này ánh xạ các chức năng được mô tả trong bản đặc tả thiết kế UI/UX ban đầu (`4. IT.OCN_DESIGN-SPEC_ACC_UIUX-SUPPLEMENT_v4.0_10042026.pdf`) vào kiến trúc Enterprise mới bao gồm **Admin Service**, **ACC Core**, **ACC Batch**, và các **Sub-ledgers (AR/AP/INV)**.

## 1. Nhóm chức năng chuyển sang Admin Service UI
Các chức năng liên quan đến thiết lập cơ cấu tổ chức và phân quyền trước đây được lên kế hoạch nằm trong module "Kế toán" (ACC) nay sẽ được dời sang giao diện quản trị trung tâm của hệ thống (Admin Portal).

- **Khai báo thông tin Doanh nghiệp**: (Tên, Mã số thuế, Logo, Địa chỉ). Giao diện này sẽ map vào API tạo `LegalEntity`.
- **Khai báo Cơ cấu tổ chức**: Tạo mới Chi nhánh, Phòng ban. Map vào API `Branch` và `Department`.
- **Phân quyền người dùng**: Gán quyền Kế toán trưởng, Kế toán viên cho các User. Map vào Identity & Permission APIs.

*Lợi ích UX*: Người quản trị chỉ cần cấu hình công ty một lần duy nhất tại màn hình Admin, sau đó toàn bộ phân hệ Kế toán, Nhân sự, Kho sẽ tự động có thông tin này.

## 2. Nhóm chức năng thuộc ACC Core UI (Kế toán Tổng hợp)
Màn hình dành riêng cho Kế toán Tổng hợp / Kế toán trưởng điều hành.

- **Khai báo Danh mục Kế toán**:
  - Màn hình Khai báo Năm tài chính (`FiscalYear`, `FiscalPeriod`).
  - Màn hình Khai báo Hệ thống Tài khoản (Tạo `Account`, set NormalBalance, check Require Dimensions).
  - Màn hình Cấu hình Quy tắc hạch toán tự động (`PostingRule`).
- **Nghiệp vụ Sổ cái**:
  - Màn hình "Lập Bút toán Thủ công" (Manual Journal Entry).
  - Màn hình "Duyệt chứng từ" (Duyệt các Journal đang ở trạng thái Pending Approval).

## 3. Nhóm chức năng thuộc ACC Batch UI (Xử lý cuối kỳ)
Màn hình chuyên dụng cho các tác vụ EOD/EOM. Có UI dạng Task Checklist (Core Banking Style).

- Màn hình **"Đóng kỳ Kế toán"**:
  - Check-list các task: Chạy Khấu hao, Chạy tính giá xuất kho, Đánh giá lại ngoại tệ.
  - Nút bấm `Lock Period` (Khóa sổ) và `Switch Business Date` (Chuyển ngày làm việc).

## 4. Nhóm chức năng thuộc Sub-ledger UI (Nghiệp vụ hàng ngày)
Các màn hình này **KHÔNG** thuộc ACC Service nữa, mà được tách ra cho từng bộ phận chuyên trách.

- **Phân hệ AP (Kế toán Mua hàng/Phải trả)**:
  - Màn hình Lập Hóa đơn Đầu vào (Purchase Invoice).
  - Màn hình Đối trừ công nợ Nhà cung cấp (AP Settlement).
- **Phân hệ AR (Kế toán Bán hàng/Phải thu)**:
  - Màn hình Lập Hóa đơn Đầu ra (Sales Invoice).
  - Màn hình Đối trừ công nợ Khách hàng (AR Settlement).
- **Phân hệ Treasury (Kế toán Ngân hàng/Quỹ)**:
  - Màn hình Nhập Sao kê, Ủy nhiệm chi (Bank Receipt / Payment).
  - Màn hình Đối soát Ngân hàng (Bank Reconciliation).
- **Phân hệ INV (Kế toán Kho)**:
  - Màn hình Nhập/Xuất kho.

## 5. Nhóm chức năng Báo cáo (ACC Report UI)
Giao diện Dashboards và xem số liệu cho Ban Giám đốc và Kế toán trưởng.

- **Báo cáo Tài chính**: Bảng Cân đối Kế toán, Báo cáo Kết quả Kinh doanh, Lưu chuyển Tiền tệ.
- **Sổ sách**: Bảng Cân đối Phát sinh (Trial Balance), Sổ Cái (Ledger), Sổ Chi tiết Tài khoản.

*Lưu ý UX*: Nhờ Materialized Views, các báo cáo này sẽ load siêu tốc. UI nên hỗ trợ thao tác "Drill-down" (Click vào một con số tổng trên Bảng CĐPS để mở popup xem chi tiết các dòng JournalLine tạo nên số đó).
