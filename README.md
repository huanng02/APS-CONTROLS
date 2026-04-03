# APS-CONTROLS – Hệ thống quản lý bãi xe tự động

Ứng dụng **WPF (.NET)** quản lý bãi xe tự động tích hợp nhận dạng biển số xe (OCR/camera), kiểm soát ra vào bằng thẻ RFID, và điều khiển barrier tự động thông qua bộ điều khiển kiểm soát cửa C3-200.

---

## Tính năng chính

- 📷 Nhận dạng biển số xe (Tesseract OCR)
- 🎴 Quản lý thẻ RFID ra/vào
- 🚧 Tích hợp điều khiển barrier tự động qua relay C3-200
- 📋 Lịch sử xe vào/ra và báo cáo
- 🔍 Tìm kiếm phương tiện theo biển số / thời gian

---

## Yêu cầu hệ thống

| Thành phần | Phiên bản |
|---|---|
| .NET Framework / .NET | 6.0+ |
| Windows | 10/11 (64-bit) |
| Camera | USB hoặc IP camera |
| Panel kiểm soát | ZKTeco C3-200 (qua TCP/IP) |

---

## Cài đặt & Chạy

```bash
# Clone repository
git clone https://github.com/huanng02/APS-CONTROLS.git

# Mở solution trong Visual Studio
APS.sln

# Build & Run (F5)
```

---

## Tài liệu kỹ thuật

| Tài liệu | Mô tả |
|---|---|
| [📡 Hướng dẫn C3-200 & Barrier](docs/c3-200-barrier.md) | Sơ đồ đấu dây, cấu hình phần mềm, xử lý sự cố cho hệ thống kiểm soát barrier tự động sử dụng panel C3-200 |

---

## Cấu trúc thư mục

```
APS-CONTROLS/
├── docs/
│   └── c3-200-barrier.md   ← Hướng dẫn C3-200 + Barrier
├── Models/                  ← Data models
├── ViewModels/              ← MVVM ViewModels
├── Views/                   ← WPF XAML Views
├── Services/                ← Business logic & services
├── tessdata/                ← OCR language data
└── APS.sln
```

---

## Đóng góp

1. Fork repository
2. Tạo branch tính năng: `git checkout -b feature/ten-tinh-nang`
3. Commit thay đổi: `git commit -m "Thêm tính năng ..."`
4. Push và tạo Pull Request

---

## Giấy phép

Xem file `LICENSE` (nếu có) hoặc liên hệ tác giả.
