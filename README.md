# Kingston USB Auto Format

Công cụ Windows dành cho kỹ thuật viên, dùng để định dạng USB thành `exFAT` theo quy trình có xác nhận và ghi nhật ký.

> **CẢNH BÁO:** Thao tác định dạng sẽ xóa toàn bộ dữ liệu trên USB đã chọn. Công cụ này không thể khôi phục dữ liệu đã xóa.

## Tính năng an toàn

- Chỉ hiển thị và chấp nhận ổ đĩa có `BusType = USB`.
- Chặn tuyệt đối `Disk 0`.
- Chặn ổ đĩa được Windows đánh dấu `IsBoot` hoặc `IsSystem`.
- Chặn ổ đĩa đang ngoại tuyến hoặc có dung lượng không hợp lệ.
- Bắt buộc kỹ thuật viên nhập số ổ đĩa, sau đó nhập chính xác `FORMAT <số ổ đĩa>`.
- Đọc lại thông tin thiết bị sau khi xác nhận và hủy nếu model, serial, Windows Unique ID hoặc dung lượng thay đổi.
- Không tự động định dạng khi cắm USB.
- Không có tham số bỏ qua bước xác nhận.
- Xác minh hệ thống tệp `exFAT` và nhãn `COMPANY-USB` sau khi DiskPart hoàn tất.

## Cách sử dụng

1. Mở `CompanyUsbFormatter.exe`.
2. Chấp nhận hộp thoại yêu cầu quyền Administrator của Windows.
3. Kiểm tra kỹ model, serial và dung lượng của USB.
4. Nhập số ổ đĩa được hiển thị.
5. Nhập chính xác chuỗi xác nhận, ví dụ `FORMAT 1`.
6. Chờ đến khi chương trình báo thành công và hiển thị ký tự ổ đĩa.

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
dist\win-x64\CompanyUsbFormatter.exe
```

Đây là file EXE `win-x64`, self-contained và single-file. Máy đích không cần cài đặt riêng .NET Runtime.

Chạy riêng bộ kiểm thử:

```powershell
dotnet run --project tests\CompanyUsbFormatter.Tests\CompanyUsbFormatter.Tests.csproj -c Release
```

## Triển khai trong công ty

Bản dựng mặc định chưa được ký số. Windows SmartScreen hoặc EDR có thể cảnh báo đối với file EXE nội bộ chưa ký. Trước khi triển khai rộng:

1. Ký Authenticode bằng chứng thư code-signing của công ty.
2. Kiểm tra mã băm SHA-256 sau khi ký.
3. Phân phối qua Intune, SCCM hoặc kênh quản lý phần mềm được phê duyệt.
4. Chỉ cấp công cụ cho nhóm kỹ thuật viên có quyền Administrator cục bộ.

Ví dụ ký số:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a .\dist\win-x64\CompanyUsbFormatter.exe
```

Cần thay URL timestamp và cách chọn certificate theo chính sách PKI của công ty.
