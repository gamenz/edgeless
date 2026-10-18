# Edgeless

App chạy nền ở system tray, tự động chuyển game đang chạy ở chế độ **Windowed** sang **Borderless Windowed**
(phủ kín màn hình, không viền), tương tự "Borderless Gaming". Một file `.exe` duy nhất, không cần cài .NET.

Thiết kế chi tiết: [docs/specs/2026-10-18-edgeless-spec.md](docs/specs/2026-10-18-edgeless-spec.md).

## An toàn với anti-cheat

Edgeless chỉ thao tác **cửa sổ** từ bên ngoài, giống một window manager (đổi style viền, vị trí, kích thước):

- không inject DLL, không hook DirectX/OpenGL/Vulkan, không đọc/ghi bộ nhớ game, không gửi phím/chuột vào game;
- chỉ mở process game với quyền `PROCESS_QUERY_LIMITED_INFORMATION` để đọc tên exe;
- nếu Windows hoặc anti-cheat từ chối truy cập, Edgeless dừng lại và báo cho bạn, không tìm cách vượt qua.

Danh sách API được phép được kiểm tra tự động (`scripts/gate.ps1`, `InteropAuditTests`). Dù vậy, **không công cụ
nào đảm bảo tuyệt đối không bị ban**: một số game cấm mọi phần mềm bên thứ ba trong điều khoản sử dụng. Hãy đọc
điều khoản của game trước khi dùng.

## Sử dụng

1. Chạy `Edgeless.exe`; icon xuất hiện ở system tray (Windows 11 có thể ẩn icon trong mũi tên **^**). App không có
   cửa sổ chính; chạy `Edgeless.exe` lần nữa sẽ mở cửa sổ cài đặt.
2. Thêm game, theo một trong hai cách:
   - Mở game ở chế độ Windowed, focus vào game, rồi chuột phải icon → **Thêm "<game>.exe" vào danh sách**
     (cách chắc chắn nhất, vì lưu đúng file exe đang chạy).
   - **Cài đặt…** → **Thêm…** → chọn file exe của game.

   Game được nhận diện theo **đường dẫn đầy đủ** của file exe, nên hai game trùng tên exe ở hai thư mục khác nhau
   không bị lẫn. Lưu ý: nếu chọn file launcher nhưng game thật chạy bằng một exe khác thì profile sẽ không khớp;
   khi đó hãy thêm game từ menu tray lúc game đang chạy. Game được chuyển sang thư mục khác → dùng **Đổi…**.
   Từ đó mỗi lần game được focus, Edgeless tự áp dụng borderless.
3. **Hotkey** `Ctrl+Alt+B` (đổi được): bật/tắt borderless cho cửa sổ đang focus, kể cả khi game chưa có trong danh sách.
4. **Cài đặt…** (hoặc double-click icon): mỗi game chọn màn hình, chế độ `fill` (phủ kín) / `center` (giữ kích thước,
   căn giữa — hợp với game render độ phân giải thấp) / `custom`, thêm `WS_POPUP`, delay áp dụng.
5. Game chạy bằng quyền admin → tray → **Khởi động lại với quyền admin**.
6. Thoát app hoặc tắt Edgeless → mọi cửa sổ được trả về trạng thái ban đầu.

| Dữ liệu | Vị trí |
|---|---|
| Cấu hình | `config.json` cạnh `Edgeless.exe` (file hỏng sẽ được sao lưu và thay bằng mặc định) |
| Log | `logs\edgeless.log` cạnh `Edgeless.exe` (xoay vòng 1 MB × 3 file) |

Edgeless là app portable: copy cả thư mục là mang theo được cấu hình. Đặt exe ở thư mục ghi được (không đặt trong
`Program Files`); nếu không ghi được, app sẽ báo lỗi và thoát.

## Hiệu năng (đo trên bản publish, 2026-10-18)

| Tiêu chí | Yêu cầu | Đo được |
|---|---|---|
| Kích thước exe (Native AOT) | < 10 MB | 3.3 MB |
| CPU khi rảnh | ~0% | 0 ms trong 60 giây |
| Private memory khi rảnh | < 30 MB | 5.6 MB (working set 17.3 MB) |

## Build

Cần .NET 10 SDK và Visual Studio Build Tools với workload "Desktop development with C++" (cho Native AOT).

```bash
dotnet test
```

```bash
dotnet publish src/Edgeless -c Release
```

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gate.ps1
```

Exe nằm ở `src/Edgeless/bin/Release/net10.0-windows/win-x64/publish/Edgeless.exe`. Test tích hợp
(`tests/Edgeless.IntegrationTests`) mở các cửa sổ thật (trong suốt) nên cần phiên desktop đang đăng nhập.

## Checklist kiểm tra thủ công

Tự động hoá đã phủ: tính style/rect, cấu hình, guard, apply/restore trên cửa sổ thật, re-apply khi game tự reset,
minimize, hotkey, cửa sổ bị huỷ, autostart (trên registry key thử nghiệm). Các mục dưới cần kiểm tra bằng tay:

- [ ] Hai màn hình, màn hình phụ ở bên trái (toạ độ âm): game phủ đúng màn hình đã chọn.
- [ ] DPI khác nhau giữa hai màn hình (100% / 150%): vị trí đúng, cửa sổ cài đặt hiển thị rõ nét.
- [ ] Game chạy bằng quyền admin, Edgeless không admin: có thông báo, không crash; "Khởi động lại với quyền admin" hoạt động.
- [ ] Bật "Khởi động cùng Windows" (Bình thường và Với quyền admin), đăng xuất rồi đăng nhập lại: app chỉ hiện ở tray.
- [ ] Đổi hotkey trong cài đặt; chọn một hotkey đang bị app khác dùng → có cảnh báo.
- [ ] Sửa tay `config.json` thành JSON hỏng → khởi động lại → có file sao lưu và thông báo.
- [ ] Khởi động lại Explorer → icon tray xuất hiện lại.
- [ ] Đăng xuất khi đang có cửa sổ borderless → cửa sổ được khôi phục.
- [ ] Vài game thật.

Muốn xem cửa sổ game giả khi test tay: `Edgeless.TestWindow.exe --visible --mode reset-once`.
