# Hướng dẫn tích hợp C3-200 điều khiển Barrier tự động

> **Phiên bản:** 1.0 | **Ngôn ngữ:** Tiếng Việt | **Đối tượng:** Kỹ thuật viên lắp đặt hệ thống kiểm soát ra vào

---

## Mục lục

1. [Giới thiệu C3-200](#1-giới-thiệu-c3-200)
2. [Cảnh báo an toàn trước khi đấu dây](#2-cảnh-báo-an-toàn-trước-khi-đấu-dây)
3. [Sơ đồ khối hệ thống](#3-sơ-đồ-khối-hệ-thống)
4. [Mô tả cọc đấu dây C3-200](#4-mô-tả-cọc-đấu-dây-c3-200)
5. [Đấu nối 1 làn (entry-only)](#5-đấu-nối-1-làn-entry-only)
6. [Đấu nối 2 làn (entry + exit)](#6-đấu-nối-2-làn-entry--exit)
7. [Đấu nối đầu đọc thẻ](#7-đấu-nối-đầu-đọc-thẻ)
8. [An toàn phương tiện – Loop detector / Photocell](#8-an-toàn-phương-tiện--loop-detector--photocell)
9. [Giám sát trạng thái barrier (feedback)](#9-giám-sát-trạng-thái-barrier-feedback)
10. [Cấu hình phần mềm (ZKAccess / BioSecurity)](#10-cấu-hình-phần-mềm-zkaccess--biosecurity)
11. [Lịch trình thời gian & Anti-passback](#11-lịch-trình-thời-gian--anti-passback)
12. [Kiểm tra sau lắp đặt](#12-kiểm-tra-sau-lắp-đặt)
13. [Xử lý sự cố thường gặp](#13-xử-lý-sự-cố-thường-gặp)

---

## 1. Giới thiệu C3-200

**C3-200** là bộ điều khiển kiểm soát ra vào (Access Control Panel) phổ biến của hãng **ZKTeco**, hỗ trợ tối đa **2 cửa / 2 làn** với các tính năng:

| Thông số | Giá trị |
|---|---|
| Số cửa điều khiển | 2 |
| Giao thức đầu đọc | Wiegand 26/34, RS-485 |
| Ngõ ra relay | 2 × relay (NO/NC/COM) |
| Ngõ vào phụ (AUX IN) | 4 |
| Nguồn cấp | 12 V DC, ~1.5 A |
| Giao tiếp host | TCP/IP (RJ-45) |
| Lưu thẻ | Tối đa 30.000 thẻ |
| Lưu sự kiện | Tối đa 100.000 bản ghi |

### Nguyên lý điều khiển barrier

```
[Thẻ/Vân tay/QR] → [Đầu đọc] → [C3-200 xác thực] → [Relay đóng ~1 s] → [Barrier nhận tín hiệu DRY-CONTACT → MỞ]
```

Barrier tự động (BFT, FAAC, Nice, Magnetic, v.v.) đều có ngõ vào **OPEN / PULSE / DRY CONTACT**: khi 2 chân này được "chập" (short), barrier thực hiện lệnh mở. C3-200 thực hiện chính xác thao tác này thông qua **tiếp điểm relay khô (dry-contact)**.

---

## 2. Cảnh báo an toàn trước khi đấu dây

> ⚠️ **ĐỌC KỸ TRƯỚC KHI THAO TÁC**

```
╔══════════════════════════════════════════════════════════════════╗
║  CẢNH BÁO AN TOÀN ĐIỆN                                          ║
║                                                                  ║
║  1. CẮT NGUỒN hoàn toàn (cả AC lưới và DC backup) trước khi    ║
║     đấu dây hoặc thay đổi kết nối terminal.                     ║
║                                                                  ║
║  2. C3-200 dùng nguồn 12 V DC. KHÔNG đấu nhầm 24 V hoặc 220 V  ║
║     vào board – sẽ cháy IC nguồn và hỏng toàn bộ panel.        ║
║                                                                  ║
║  3. Barrier motor thường chạy 230 V AC hoặc 24 V DC nội bộ.    ║
║     Relay C3-200 chỉ kết nối phần DRY-CONTACT (cách ly điện),  ║
║     KHÔNG NỐI trực tiếp dây AC vào relay panel.                 ║
║                                                                  ║
║  4. Tiếp đất (GND) cho tủ điện đúng quy chuẩn để chống sét     ║
║     và nhiễu cảm ứng từ motor barrier.                          ║
║                                                                  ║
║  5. Dùng cầu dao/MCB riêng cho mạch điều khiển và mạch động lực║
║     của barrier.                                                 ║
╚══════════════════════════════════════════════════════════════════╝
```

---

## 3. Sơ đồ khối hệ thống

```
                        ┌─────────────────────────────────┐
                        │          C3-200 Panel            │
                        │                                  │
  ┌──────────┐ Wiegand  │  READER1 ────────────────────   │
  │ Đầu đọc  │─────────►│  D0/D1/GND/+12V                │
  │  thẻ vào │          │           │                     │
  └──────────┘          │           ▼                     │
                        │  [Xác thực & Logic]             │
  ┌──────────┐ Wiegand  │  READER2 ────────────────────   │
  │ Đầu đọc  │─────────►│  D0/D1/GND/+12V                │
  │  thẻ ra  │          │           │                     │
  └──────────┘          │           ▼                     │
                        │  RELAY1 (NO/COM) ──────────────►│──► Barrier Vào (OPEN)
                        │  RELAY2 (NO/COM) ──────────────►│──► Barrier Ra (OPEN)
                        │                                  │
  ┌──────────┐  DRY     │  AUX IN 1 ◄─────────────────── │◄── Loop Detector Vào
  │ Phát hiện│  CONTACT │  AUX IN 2 ◄─────────────────── │◄── Loop Detector Ra
  │  vòng từ │─────────►│  AUX IN 3 ◄─────────────────── │◄── Trạng thái Barrier Vào
  └──────────┘          │  AUX IN 4 ◄─────────────────── │◄── Trạng thái Barrier Ra
                        │                                  │
                        │  TCP/IP ────────────────────────►│──► PC Quản lý / APS-CONTROLS
                        └─────────────────────────────────┘
```

---

## 4. Mô tả cọc đấu dây C3-200

### Hình ảnh tham khảo

> Hình 1 – Mặt trước / bo mạch C3-200 với các cọc đấu dây được đánh số:

![C3-200 Panel – Mặt trước và cọc đấu dây](../docs/images/c3200-panel.jpg)

> Hình 2 – Nhãn terminal / sơ đồ đấu nối chi tiết:

![C3-200 Terminal Labels – Nhãn cọc đấu dây](../docs/images/c3200-wiring.jpg)

*(Nếu chưa có file ảnh, xem mô tả bảng bên dưới để xác định đúng cọc đấu dây trên thiết bị thực tế của bạn.)*

### Bảng cọc đấu dây chính (bo mạch C3-200 tiêu chuẩn)

#### Khối nguồn & GND

| Ký hiệu trên board | Chức năng | Ghi chú |
|---|---|---|
| `+12V` / `PWR` | Đầu vào nguồn dương | 12 V DC ±10% |
| `GND` / `0V` | Đất chung | Nối chung với GND nguồn |

#### Khối đầu đọc Door 1 (Cửa/Làn 1)

| Ký hiệu | Chức năng |
|---|---|
| `+12V` | Nguồn cấp cho đầu đọc (max 500 mA/cổng) |
| `GND` | Đất |
| `D0` | Wiegand Data 0 (dây xanh lá) |
| `D1` | Wiegand Data 1 (dây trắng) |
| `BZ` | Buzzer điều khiển từ panel (tuỳ đầu đọc) |
| `LED` | Đèn LED đầu đọc (tuỳ đầu đọc) |
| `TAMPER` | Chống phá (nối GND = bình thường) |

#### Khối đầu đọc Door 2 (Cửa/Làn 2) – ký hiệu tương tự

#### Khối Relay Output

| Ký hiệu | Chức năng |
|---|---|
| `RELAY1-COM` | Chân chung Relay 1 |
| `RELAY1-NO` | Thường hở – đóng khi kích (dùng cho OPEN barrier) |
| `RELAY1-NC` | Thường đóng – mở khi kích |
| `RELAY2-COM` | Chân chung Relay 2 |
| `RELAY2-NO` | Thường hở – đóng khi kích |
| `RELAY2-NC` | Thường đóng – mở khi kích |

> **Lưu ý:** Relay C3-200 là tiếp điểm khô (dry contact), chịu tải tối đa khoảng **30 V DC / 1 A** hoặc **125 V AC / 0.5 A**. Với barrier công nghiệp có dòng kích cao hơn, sử dụng thêm relay trung gian (intermediate relay 12 V DC coil).

#### Khối AUX Input (ngõ vào phụ)

| Ký hiệu | Chức năng mặc định |
|---|---|
| `IN1` | Exit button Cửa 1 (nút mở từ bên trong) |
| `IN2` | Door sensor Cửa 1 (cảm biến vị trí cửa) |
| `IN3` | Exit button Cửa 2 |
| `IN4` | Door sensor Cửa 2 |
| `GND` | Đất chung ngõ vào |

> Các AUX Input có thể tái cấu hình trong phần mềm (tuỳ firmware).

---

## 5. Đấu nối 1 làn (entry-only)

### Sơ đồ đấu dây – 1 làn vào

```
Nguồn 12 V DC
    (+) ──────────────────────────── [C3-200: +12V]
    (-) ──────────────────────────── [C3-200: GND]

Đầu đọc thẻ (Wiegand 26/34)
    Đỏ   (+12V) ──────────────────── [C3-200: READER1 +12V]
    Đen  (GND)  ──────────────────── [C3-200: READER1 GND]
    Xanh lá (D0) ─────────────────── [C3-200: READER1 D0]
    Trắng   (D1) ─────────────────── [C3-200: READER1 D1]

Barrier – Ngõ vào OPEN (dry contact)
    OPEN_A ────────────────────────── [C3-200: RELAY1-NO]
    OPEN_B ────────────────────────── [C3-200: RELAY1-COM]

Nút EXIT thủ công (nếu có)
    Chân 1 ────────────────────────── [C3-200: IN1]
    Chân 2 ────────────────────────── [C3-200: GND]
```

**Bảng tóm tắt 1 làn:**

| Từ | Đến | Dây | Ghi chú |
|---|---|---|---|
| Nguồn (+) 12 V | C3-200 `+12V` | Đỏ, ≥1 mm² | Qua cầu chì 2 A |
| Nguồn (−) 12 V | C3-200 `GND` | Đen, ≥1 mm² | |
| Reader `+12V` | C3-200 `READER1 +12V` | Đỏ | |
| Reader `GND` | C3-200 `READER1 GND` | Đen | |
| Reader `D0` | C3-200 `READER1 D0` | Xanh lá | |
| Reader `D1` | C3-200 `READER1 D1` | Trắng | |
| Barrier `OPEN_A` | C3-200 `RELAY1-NO` | Bất kỳ | Dry contact |
| Barrier `OPEN_B` | C3-200 `RELAY1-COM` | Bất kỳ | Dry contact |
| Exit button | C3-200 `IN1` + `GND` | 2 dây | Nối tắt = mở |

---

## 6. Đấu nối 2 làn (entry + exit)

### Sơ đồ đấu dây – 2 làn (vào + ra)

```
Nguồn 12 V DC
    (+) ───── [C3-200: +12V]
    (-) ───── [C3-200: GND]

Làn VÀO:
    Đầu đọc thẻ vào ──── READER1 (D0/D1/+12V/GND)
    Barrier vào OPEN ─── RELAY1-NO + RELAY1-COM
    Exit button vào ──── IN1 + GND

Làn RA:
    Đầu đọc thẻ ra ───── READER2 (D0/D1/+12V/GND)
    Barrier ra OPEN ──── RELAY2-NO + RELAY2-COM
    Exit button ra ───── IN3 + GND
```

**Bảng tóm tắt 2 làn:**

| Làn | Đầu đọc | Relay Output | Exit Button |
|---|---|---|---|
| Vào (Door 1) | READER1 | RELAY1-NO / RELAY1-COM | IN1 + GND |
| Ra (Door 2) | READER2 | RELAY2-NO / RELAY2-COM | IN3 + GND |

> **Tip 2 làn:** Sử dụng **Anti-passback** để chống mượn thẻ: thẻ vào không thể vào lần nữa mà phải ra trước (xem mục 11).

---

## 7. Đấu nối đầu đọc thẻ

### Wiegand (phổ biến nhất)

| Màu dây tiêu chuẩn | Ký hiệu | Kết nối |
|---|---|---|
| Đỏ | VCC | READER +12V |
| Đen | GND | READER GND |
| Xanh lá | D0 | READER D0 |
| Trắng | D1 | READER D1 |
| Nâu (tuỳ model) | BZ | READER BZ (buzzer) |
| Cam (tuỳ model) | LED | READER LED |

> Cáp Wiegand nên dùng **cáp tín hiệu xoắn đôi (twisted pair)** dài tối đa **150 m**. Nếu cần đi xa hơn, dùng **Wiegand extender** hoặc chuyển sang RS-485.

### RS-485 (nếu đầu đọc hỗ trợ)

| Ký hiệu | Kết nối C3-200 | Ghi chú |
|---|---|---|
| RS485+ | RS485 A+ | Cáp shielded twisted pair |
| RS485− | RS485 B− | |
| GND | GND | Nối đất 1 điểm |

---

## 8. An toàn phương tiện – Loop detector / Photocell

> **Bắt buộc** phải có thiết bị phát hiện phương tiện để tránh hạ barrier khi xe đang đi qua.

### Nguyên tắc hoạt động

```
[Loop detector / Photocell] ──→ [Ngõ vào SAFE của Barrier]
         ↓
  Khi xe còn trong vùng → barrier KHÔNG hạ (lệnh CLOSE bị giữ lại)
  Khi xe qua hẳn → barrier hạ theo thời gian đặt trước
```

> **Khuyến nghị:** Để barrier xử lý logic an toàn nội bộ (không qua C3-200). Kết nối loop detector / photocell **thẳng vào ngõ vào SAFE/LOOP của chính board barrier**, không qua relay panel.

### Tùy chọn – Dùng AUX INPUT của C3-200 để giám sát

Nếu phần mềm quản lý cần biết có xe hay không:

| Từ | Đến C3-200 | Ghi chú |
|---|---|---|
| Loop detector `OUT` (dry contact) | `IN2` + `GND` | IN2 = "có xe" khi đóng |
| Photocell `FAULT` output | `IN4` + `GND` | IN4 = "bị che" khi đóng |

---

## 9. Giám sát trạng thái barrier (feedback)

Nếu barrier có ngõ ra trạng thái (status output), nối về AUX INPUT của C3-200 để phần mềm theo dõi:

| Trạng thái | Ngõ ra Barrier | Nối về C3-200 |
|---|---|---|
| Barrier đang MỞ | `OPEN_STATUS` COM/NO | `IN2` + `GND` |
| Barrier đang ĐÓNG | `CLOSE_STATUS` COM/NO | `IN4` + `GND` |
| Lỗi / Overload | `FAULT` output | Tùy cấu hình |

> Cấu hình AUX Input trong phần mềm ZKAccess: **Device → Door → Sensor Type → "Normally Open"** hoặc **"Normally Closed"** tùy logic.

---

## 10. Cấu hình phần mềm (ZKAccess / BioSecurity)

### 10.1 Thêm thiết bị C3-200

1. Mở ZKAccess / ZKBioSecurity → **Device** → **Add**
2. Nhập địa chỉ IP của C3-200 (mặc định: `192.168.1.201`)
3. Chọn **Communication Type**: TCP/IP
4. Nhấn **Connect** – đèn kết nối trên panel nhấp nháy xác nhận
5. **Synchronize time** sau khi kết nối

### 10.2 Cấu hình cửa (Door Settings)

| Tham số | Giá trị khuyến nghị | Ghi chú |
|---|---|---|
| **Door Name** | `Barrier Vào` / `Barrier Ra` | Đặt tên mô tả |
| **Lock Driver Time** | `500 ms` – `1000 ms` | Thời gian relay đóng (= thời gian xung mở barrier) |
| **Door Sensor** | `No Sensor` hoặc `Normally Open` | Tuỳ có/không dây feedback |
| **Door Sensor Delay** | `5 s` | Thời gian chờ trước khi báo "door not closed" |
| **Verify Mode** | `Card Only` / `Card + PIN` | Tuỳ yêu cầu bảo mật |

> **Lock Driver Time (Thời gian xung relay)** rất quan trọng:  
> - Hầu hết barrier nhận xung **≥ 200 ms** để kích mở.  
> - Giá trị an toàn thông thường: **500 ms**.  
> - Nếu barrier không nhận: tăng lên **1000 ms** và kiểm tra lại.

### 10.3 Thêm đầu đọc (Reader)

1. **Device → Door → Reader** → chọn **Reader 1** (vào) hoặc **Reader 2** (ra)
2. Đặt **Reader Type**: `Wiegand 26` hoặc `Wiegand 34` tùy đầu đọc
3. Bật **Verify Mode** phù hợp

---

## 11. Lịch trình thời gian & Anti-passback

### 11.1 Time Schedule (Lịch trình giờ mở)

Cho phép chỉ mở cổng trong khung giờ xác định:

```
Ví dụ Time Schedule – Giờ hành chính:
  Thứ 2 – Thứ 6:  07:00 → 18:00  (mở bằng thẻ)
  Thứ 7:          07:00 → 12:00
  Chủ nhật:       Không mở (ALL DAY CLOSED)
```

**Cách tạo trong ZKAccess:**
1. **Access Control → Time Schedule → Add**
2. Đặt tên: `Gio Hanh Chinh`
3. Chọn ngày và giờ tương ứng
4. **Access Control → Access Level → Add** → gán Time Schedule cho nhóm thẻ

### 11.2 Anti-passback (APB)

Anti-passback ngăn một thẻ dùng 2 lần liên tiếp ở cùng một chiều (chống mượn thẻ):

| Loại APB | Mô tả |
|---|---|
| **Hard APB** | Thẻ vào rồi phải ra mới được vào lại (hệ thống từ chối tuyệt đối) |
| **Soft APB** | Ghi cảnh báo nhưng vẫn cho qua |
| **Timed APB** | Sau X phút tự reset, bất kể đã ra chưa |

**Cấu hình:**
1. **Access Control → Anti-Passback → Enable**
2. Gán **Door 1 = Entry**, **Door 2 = Exit**
3. Chọn loại: **Hard** (khuyến nghị cho bãi xe)

### 11.3 First-Card-Normally-Open (FCNO)

Tính năng "thẻ đầu tiên mở cổng tự do": sau khi thẻ được uỷ quyền quẹt lần đầu trong ca, barrier tự mở cho tất cả xe không cần thẻ đến hết ca.

> Thường dùng cho cổng vào ban ngày tại cơ quan/trường học.

---

## 12. Kiểm tra sau lắp đặt

### Checklist kiểm tra (✓ = Đạt)

```
[ ] 1. Nguồn 12 V DC đo tại đầu vào panel: 11.5 V – 13.5 V
[ ] 2. Panel kết nối được với phần mềm qua TCP/IP
[ ] 3. Đồng bộ giờ giữa panel và server
[ ] 4. Đầu đọc thẻ nhận dạng thẻ thử nghiệm, log hiện trong phần mềm
[ ] 5. Quẹt thẻ hợp lệ → relay đóng (nghe tiếng click) → barrier mở
[ ] 6. Thời gian relay đóng đúng theo cấu hình (đo bằng đồng hồ/đèn LED)
[ ] 7. Barrier hạ trở lại sau thời gian đặt (không bị kẹt)
[ ] 8. Loop detector / photocell giữ barrier không hạ khi có xe trong vùng
[ ] 9. Nút EXIT thủ công (IN1/IN3) mở được barrier
[ ] 10. Anti-passback hoạt động đúng (thẻ thứ 2 liên tiếp bị từ chối)
[ ] 11. Thử mất điện: UPS/backup cấp đủ cho panel + barrier
[ ] 12. Kiểm tra log sự kiện lưu đủ trong phần mềm
```

---

## 13. Xử lý sự cố thường gặp

| Triệu chứng | Nguyên nhân có thể | Cách xử lý |
|---|---|---|
| Barrier không mở dù thẻ hợp lệ | Relay không đóng | Đo thông mạch RELAY1-NO và RELAY1-COM khi kích; kiểm tra cấu hình Lock Driver Time |
| Barrier không mở dù relay đã đóng | Đấu sai cọc barrier | Kiểm tra đúng chân OPEN_A/OPEN_B trên bảng mạch barrier; thử dùng dây nối tắt 2 chân OPEN |
| Đầu đọc không nhận thẻ | Sai Wiegand format | Đổi từ W26 → W34 trong phần mềm hoặc ngược lại |
| Đầu đọc không sáng/không phản hồi | Mất nguồn hoặc đứt dây | Đo điện áp tại READER +12V/GND; kiểm tra cáp |
| Panel không kết nối phần mềm | Sai IP hoặc firewall | Ping địa chỉ IP panel; kiểm tra cùng subnet; tắt firewall thử |
| Log không đồng bộ | Lệch giờ panel/server | Synchronize time trong phần mềm |
| Anti-passback báo sai | Đổi chiều IN/OUT nhầm | Kiểm tra lại Door 1 = Entry, Door 2 = Exit trong cấu hình APB |
| Barrier hạ khi xe đang trong vùng | Loop detector chưa đấu | Nối đầu ra loop/photocell vào ngõ SAFE của barrier (không qua panel) |
| Relay bị nóng / hỏng | Dòng tải quá lớn | Dùng relay trung gian 12 V DC + contactor cho barrier công nghiệp |

---

## Phụ lục – Thông số kỹ thuật relay C3-200

| Thông số | Giá trị |
|---|---|
| Loại | Dry contact (tiếp điểm khô) |
| Tải DC tối đa | 30 V DC / 1 A |
| Tải AC tối đa | 125 V AC / 0.5 A |
| Thời gian phản hồi | < 10 ms |
| Tuổi thọ cơ học | > 10 triệu lần đóng mở |

> Nếu barrier yêu cầu dòng kích cao hơn, sử dụng relay trung gian (ví dụ: Omron MY2N-GS 12VDC) đặt trong tủ điều khiển.

---

*Tài liệu này thuộc dự án **APS-CONTROLS** – Hệ thống quản lý bãi xe tự động.*  
*Xem thêm: [README chính](../README.md)*
