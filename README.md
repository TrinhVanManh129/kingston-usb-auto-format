# Kingston USB Auto Format

Công cụ Windows có giao diện đồ họa dành cho kỹ thuật viên, dùng để định dạng USB thành `exFAT` theo quy trình có xác nhận và ghi nhật ký.

> **CẢNH BÁO:** Thao tác định dạng sẽ xóa toàn bộ dữ liệu trên USB đã chọn. Công cụ này không thể khôi phục dữ liệu đã xóa.

## Tính năng an toàn

- Chỉ hiển thị và chấp nhận ổ đĩa có `BusType = USB`.
- Chặn tuyệt đối `Disk 0`.
- Chặn ổ đĩa được Windows đánh dấu `IsBoot` hoặc `IsSystem`.
- Chặn ổ đĩa đang ngoại tuyến hoặc có dung lượng không hợp lệ.
- Bắt buộc kỹ thuật viên chọn USB và nhập lại chính xác số `Disk` trước khi nút định dạng được bật.
- Đọc lại thông tin thiết bị sau khi xác nhận và hủy nếu model, serial, Windows Unique ID hoặc dung lượng thay đổi.
- Không tự động định dạng khi cắm USB.
- Không có tham số bỏ qua bước xác nhận.
- Xác minh hệ thống tệp `exFAT` và nhãn `COMPANY-USB` sau khi DiskPart hoàn tất.

## Cách sử dụng

1. Mở `KingstonUsbFormatter.exe`.
2. Chấp nhận hộp thoại yêu cầu quyền Administrator của Windows.
3. Chọn USB trong bảng thiết bị.
4. Kiểm tra kỹ model, serial, dung lượng và ổ đĩa hiện tại.
5. Nhập lại số `Disk` vào ô xác nhận, ví dụ `1`.
6. Nhấn **FORMAT USB** và xác nhận cảnh báo cuối cùng.
7. Chờ đến khi chương trình báo thành công và hiển thị ký tự ổ đĩa.

Kết quả mặc định:

- Kiểu phân vùng: `MBR`
- Một phân vùng chính sử dụng toàn bộ dung lượng
- Hệ thống tệp: `exFAT`
- Nhãn ổ đĩa: `COMPANY-USB`
- Định dạng nhanh

## Nhật ký

Nhật ký CSV được ghi theo từng tháng tại:

```text
%ProgramData%\CompanyUsbFormatter\Logs\YYYY-MM.csv
```

Nếu không thể ghi vào thư mục trên, chương trình sử dụng thư mục `Logs` nằm cạnh file EXE.

Nhật ký gồm thời gian UTC, tên máy, tài khoản Windows, số ổ đĩa, model, serial, dung lượng, kết quả, ký tự ổ đĩa và thông báo. Công cụ không đọc hoặc ghi tên tệp trên USB vào nhật ký.

## Mã thoát

| Mã | Ý nghĩa |
|---:|---|
| 0 | Định dạng và xác minh thành công |
| 1 | Không tìm thấy USB |
| 2 | Lựa chọn ổ đĩa không hợp lệ |
| 3 | Người dùng hủy thao tác |
| 4 | Bị chặn bởi quy tắc an toàn |
| 5 | DiskPart thất bại |
| 6 | Không xác minh được ổ đĩa sau khi định dạng |
| 10 | Lỗi không mong đợi |

## Cấu trúc mã nguồn

```text
src/CompanyUsbFormatter/
  Program.cs                  Điểm khởi chạy và ghép các thành phần
  MainForm.cs                 Giao diện WinForms và luồng thao tác
  UsbFormattingWorkflow.cs    Luồng chọn, xác nhận, định dạng và xác minh
  DiskSafetyValidator.cs      Quy tắc chống xóa nhầm ổ đĩa
  PowerShellDiskInventory.cs  Đọc thông tin ổ đĩa từ Windows
  DiskPartFormatter.cs        Tạo và chạy tập lệnh DiskPart
  AuditLogger.cs              Ghi nhật ký CSV
  SystemCommandRunner.cs      Chạy tiến trình với thời gian chờ
  Abstractions.cs             Interface phục vụ kiểm thử an toàn
  Models.cs                   Các kiểu dữ liệu
tests/CompanyUsbFormatter.Tests/
  Program.cs                  Bộ kiểm thử không phụ thuộc gói bên ngoài
build.ps1                     Kiểm thử và xuất bản EXE
```

## Biên dịch

Yêu cầu đối với máy biên dịch:

- Windows
- .NET 8 SDK hoặc mới hơn

Chạy:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

File EXE được tạo tại:

```text
dist\win-x64\KingstonUsbFormatter.exe
```

Đây là file EXE `win-x64`, self-contained và single-file. Máy đích không cần cài đặt riêng .NET Runtime.

Chạy riêng bộ kiểm thử:

```powershell
dotnet run --project tests\CompanyUsbFormatter.Tests\CompanyUsbFormatter.Tests.csproj -c Release
```
