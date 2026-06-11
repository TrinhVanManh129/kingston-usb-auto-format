# Company USB Formatter

Cong cu Windows danh cho ky thuat vien, dung de format USB thanh `exFAT` theo quy trinh co xac nhan va ghi log.

> **CANH BAO:** Format se xoa toan bo du lieu tren USB da chon. Du lieu khong the khoi phuc bang cong cu nay.

## Tinh nang an toan

- Chi hien va chap nhan disk co `BusType = USB`.
- Chan tuyet doi `Disk 0`.
- Chan disk duoc Windows danh dau `IsBoot` hoac `IsSystem`.
- Chan disk offline hoac khong co dung luong hop le.
- Bat ky thuat vien nhap so disk, sau do nhap chinh xac `FORMAT <so disk>`.
- Doc lai thong tin thiet bi sau khi xac nhan va huy neu model, serial, Windows unique ID hoac dung luong thay doi.
- Khong co che do tu dong format khi cam USB.
- Khong co tham so bo qua buoc xac nhan.
- Xac minh volume `exFAT` va nhan `COMPANY-USB` sau khi DiskPart ket thuc.

## Su dung

1. Mo `CompanyUsbFormatter.exe`.
2. Chap nhan hop thoai UAC Administrator.
3. Xem ky model, serial va dung luong cua USB.
4. Nhap so disk duoc hien thi.
5. Nhap chinh xac chu xac nhan, vi du `FORMAT 1`.
6. Cho den khi chuong trinh bao thanh cong va hien ky tu o dia.

Ket qua mac dinh:

- Partition style: `MBR`
- Mot primary partition su dung toan bo dung luong
- Filesystem: `exFAT`
- Volume label: `COMPANY-USB`
- Quick format

## Log

Log CSV duoc ghi theo thang tai:

```text
%ProgramData%\CompanyUsbFormatter\Logs\YYYY-MM.csv
```

Neu thu muc tren khong ghi duoc, chuong trinh dung thu muc `Logs` nam canh EXE.

Log gom thoi gian UTC, ten may, tai khoan Windows, disk number, model, serial, dung luong, ket qua, ky tu o dia va thong bao. Cong cu khong doc hay ghi ten tep noi dung tren USB vao log.

## Ma thoat

| Ma | Y nghia |
|---:|---|
| 0 | Format va xac minh thanh cong |
| 1 | Khong tim thay USB |
| 2 | Lua chon disk khong hop le |
| 3 | Nguoi dung huy |
| 4 | Bi chan boi quy tac an toan |
| 5 | DiskPart that bai |
| 6 | Khong xac minh duoc volume sau format |
| 10 | Loi khong mong doi |

## Cau truc ma nguon

```text
src/CompanyUsbFormatter/
  Program.cs                  Entry point va ghep cac thanh phan
  UsbFormattingWorkflow.cs    Luong chon, xac nhan, format, xac minh
  DiskSafetyValidator.cs      Quy tac chong xoa nham
  PowerShellDiskInventory.cs  Doc disk/volume tu Windows
  DiskPartFormatter.cs        Tao va chay script DiskPart
  AuditLogger.cs              Ghi log CSV
  SystemCommandRunner.cs      Chay tien trinh co timeout
  Abstractions.cs             Interface de test khong cham disk that
  Models.cs                   Kieu du lieu
tests/CompanyUsbFormatter.Tests/
  Program.cs                  Bo test khong phu thuoc package ngoai
build.ps1                     Test va publish EXE
```

## Build

Yeu cau may build:

- Windows
- .NET 8 SDK hoac moi hon

Chay:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

EXE duoc tao tai:

```text
dist\win-x64\CompanyUsbFormatter.exe
```

Day la EXE `win-x64` self-contained, single-file. May dich khong can cai .NET runtime rieng.

Chay rieng test:

```powershell
dotnet run --project tests\CompanyUsbFormatter.Tests\CompanyUsbFormatter.Tests.csproj -c Release
```

## Trien khai cong ty

Ban build mac dinh chua duoc ky so. Windows SmartScreen hoac EDR co the canh bao voi EXE noi bo chua ky. Truoc khi trien khai rong:

1. Ky Authenticode bang chung thu code-signing cua cong ty.
2. Kiem tra hash SHA-256 sau khi ky.
3. Phan phoi qua Intune, SCCM hoac kenh quan ly phan mem duoc phe duyet.
4. Gioi han cong cu cho nhom ky thuat vien duoc cap quyen local Administrator.

Vi du ky so:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a .\dist\win-x64\CompanyUsbFormatter.exe
```

Can thay URL timestamp va cach chon certificate theo chinh sach PKI cua cong ty.
