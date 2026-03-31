# CEO Decision & Sprint Plan

**Ngày:** 2026-03-31
**Căn cứ:** Code Review Report + Feedback từ nhân sự kỹ thuật

---

## Đánh giá tình hình

Nhân sự kỹ thuật đã review đúng trọng tâm. Tôi đồng ý với phần lớn nhận định, và muốn bổ sung góc nhìn chiến lược trước khi đưa ra plan.

**Tình trạng hiện tại của sản phẩm:**

Codebase đang ở giai đoạn "có thể demo được, chưa thể vận hành thật". Nền tảng kiến trúc ổn — nhưng ba lỗ hổng nghiệp vụ có thể gây ra incident ngay ngày đầu tiên khách sạn thật sử dụng:

1. Duplicate booking sau khi khách nói `cảm ơn`
2. Double booking do race condition
3. Mọi tin nhắn đều đốt token AI, kể cả `ok`

Nếu để nguyên và release → một khách sạn thật trải nghiệm incident → mất trust → rất khó lấy lại. Đây là rủi ro chiến lược lớn hơn bất kỳ tính năng nào chưa có.

---

## Điều chỉnh so với Code Review gốc

Tôi tiếp nhận feedback từ nhân sự kỹ thuật và điều chỉnh như sau:

**1. Về "2 AI calls mỗi message"**

Không fix bằng rule cứng blanket. Hướng đúng hơn:
- Cheap-path cho low-value messages trước
- Giữ intent analysis cho message có giá trị nghiệp vụ
- Không tối ưu quá sớm phần này vì sẽ làm phức tạp code

**2. Về `[BOOKING_READY]` marker**

Đây là symptom, không phải root cause. Root cause là session giữ draft cũ + không có booking completed state. Fix root cause trước, marker sẽ tự mất rủi ro.

**3. Về idempotency**

Nhân sự đúng: `IMemoryCache` đơn thuần chỉ giảm rủi ro trong single instance. Cần thiết kế đúng:
- Giai đoạn này: cache key + lock đủ để giảm rủi ro trong phạm vi MVP
- Giai đoạn scale: cần distributed lock hoặc database-level constraint

**4. Điểm bổ sung quan trọng nhất từ nhân sự: 3-tier memory**

Report gốc đề cập nhưng chưa nhấn đủ. Đây là phần quyết định chatbot có "thực sự thông minh" khi user quay lại hay không. Tôi sẽ đưa lên thành sprint riêng.

---

## Quyết định chiến lược

**Mục tiêu trong 4 tuần tới:** Đưa sản phẩm từ "demo được" lên "vận hành được với 1-2 khách sạn thật".

Không cố làm tất cả. Làm ít, làm đúng, làm chắc.

---

## Sprint Plan

### Sprint 1 — "Make It Safe" (Tuần 1-2)

**Mục tiêu:** Loại bỏ mọi rủi ro có thể gây incident booking ngay ngày đầu.

Không release gì mới cho đến khi Sprint 1 xong.

#### Task 1.1 — Hậu-booking state guard `[P0 — Critical]`

**Việc làm:**
- Thêm `BookingSessionStatus` enum vào `ConversationSession`: `None | InProgress | Completed`
- Sau booking thành công: set `Completed`, clear `BookingDraft`, lưu `LastBookingId`
- Đầu `ProcessMessageAsync`: nếu `Completed` thì skip toàn bộ booking flow
- Log rõ khi guard được kích hoạt

**Định nghĩa done:**
- Nhắn `cảm ơn` / `ok` / `dạ` sau booking → bot trả lời tự nhiên, không tạo booking mới
- Có log dòng `[BookingGuard] Session {id} already completed, skipping booking flow`

---

#### Task 1.2 — Cheap-path cho low-value messages `[P0 — Critical]`

**Việc làm:**
- Thêm `IsLowValueMessage(string message)` rule-based vào `ConversationService`
- Nếu low-value và `BookingSessionStatus == Completed` → trả lời template, skip cả 2 AI calls
- Nếu low-value và session đang browsing → vẫn route nhẹ, không cần intent analysis
- Định nghĩa low-value: message < 15 ký tự thuần xã giao (`ok`, `dạ`, `vâng`, `cảm ơn`, `thanks`, `hello`, `hi`, `chào`, `?`, `😊`)

**Định nghĩa done:**
- Các tin trên không trigger AI call
- AI usage log không có entry cho những tin này
- Vẫn trả lời tự nhiên và phù hợp ngữ cảnh

---

#### Task 1.3 — Booking idempotency `[P0 — Critical]`

**Việc làm:**
- Tạo idempotency key: `SHA256(sessionId + roomId + checkInDate.ToString("yyyyMMdd"))`
- Trước khi tạo booking: lock key trong `IMemoryCache`, TTL 5 phút
- Nếu key đã tồn tại: trả về bookingId cũ, không gọi Sheets
- Log khi idempotency guard block request

**Phạm vi giai đoạn này:** Single-instance guard. Chấp nhận giới hạn này, ghi note để làm distributed lock khi scale.

**Định nghĩa done:**
- 2 request đồng thời với cùng session + room + ngày → chỉ 1 booking được tạo
- Log dòng `[IdempotencyGuard] Duplicate booking blocked for key {key}`

---

#### Task 1.4 — Fix `new Random()` → `Random.Shared` `[P0 — Quick Fix]`

1 dòng. Làm ngay.

---

#### Task 1.5 — `pageId → hotelId` mapping `[P0 — Infra]`

**Việc làm:**
- Thêm `Dictionary<string, string> PageMappings` vào `MessengerOptions`
- Thêm method `ResolveHotelId(string pageId)` → lookup mapping, fallback về `DefaultHotelId`
- Apply trong `TrySendChatbotReplyAsync`

**Config mẫu:**
```json
"Messenger": {
  "PageMappings": {
    "PAGE_ID_HOTEL_A": "hotel_a",
    "PAGE_ID_HOTEL_B": "hotel_b"
  },
  "DefaultHotelId": "default"
}
```

**Định nghĩa done:**
- Khi có mapping: đúng hotel
- Khi không có mapping: fallback về default, log warning

---

### Sprint 2 — "Make It Smart" (Tuần 3)

**Mục tiêu:** Giảm lỗi nghiệp vụ và tăng chất lượng chatbot trong ngày thường.

#### Task 2.1 — 3-tier memory architecture `[P1 — Strategic]`

Đây là task quan trọng nhất Sprint 2. Không có cái này, chatbot không "thông minh thật".

**3 tầng cần tách rõ:**

**Tầng 1 — Short-term Session** (đang có, giữ nguyên)
- Giữ context hội thoại gần
- Expire 30 phút
- Key: `session_{sessionId}`

**Tầng 2 — Active Booking Context** (chưa có, cần làm)
- Gắn với booking đang hiệu lực
- Expire theo `CheckOutDate + 3 ngày`
- Key: `booking_ctx_{channelType}_{senderId}` (không phụ thuộc session)
- Chứa: `BookingId`, `HotelId`, `CheckInDate`, `CheckOutDate`, `GuestName`, `GuestPhone`
- Load khi session mới bắt đầu: nếu còn active booking context → bot biết user đang có booking

**Tầng 3 — Customer Memory** (làm sau, Sprint 3+)
- Lưu dài hạn: `senderId → customerId`, booking history, tên/phone
- Cần thiết kế kỹ hơn, chưa làm sprint này

**Việc làm Sprint 2:**
- Thêm `ActiveBookingContext` class vào `DTOs.cs`
- Sau booking thành công: lưu `booking_ctx_{senderId}` vào cache, TTL = `CheckOutDate + 3 ngày`
- Đầu `ProcessMessageAsync`: load active booking context nếu có
- Nếu có active context: inject vào system prompt (`Khách đang có booking active: {bookingId}, check-in {date}`)
- Intent `booking_lookup` / `post_booking_support` → dùng active context, không hỏi lại

**Định nghĩa done:**
- User quay lại sau 2 giờ hỏi `booking của tôi sao rồi` → bot trả lời đúng mã booking
- Session mới vẫn biết booking context từ lần trước

---

#### Task 2.2 — Google Sheets caching `[P1 — Performance]`

**Việc làm:**
- Cache trong `GoogleSheetsService` dùng `IMemoryCache`
- TTL theo loại data:
  - `HotelInfo`: 15 phút
  - `Rooms`, `Pricing`, `Amenities`, `FAQ`: 10 phút
  - `Promotions`: 10 phút
  - `Availability`: 2 phút
  - `Holidays`: 60 phút
- Cache key: `sheets_{hotelId}_{dataType}`
- Sau `CreateBookingAsync` và `UpdateAvailabilityAsync`: invalidate availability cache

**Định nghĩa done:**
- Nhiều message liên tiếp trong cùng session không trigger nhiều Sheets API call
- Có log khi cache hit vs miss
- Booking vừa tạo xong → availability query tiếp theo phản ánh đúng (không phục vụ data cũ)

---

#### Task 2.3 — Temporal parser mở rộng `[P1 — Accuracy]`

**Thêm các pattern:**
- `mai` → `today + 1`
- `mốt` → `today + 2`
- `ngày kia` → `today + 3`
- `cuối tuần này` → Saturday tới gần nhất
- `cuối tuần tới` → Saturday tuần sau
- `5/4`, `05/04`, `5-4`, `5 tháng 4` → parse absolute date
- `từ 5 đến 7 tháng 4` → check_in = 5/4, check_out = 7/4
- `ở 2 đêm từ thứ 7` → check_in = thứ 7 tới, check_out = check_in + 2

**Nguyên tắc:** Parser deterministic override AI — nếu parser tìm thấy ngày → dùng luôn, không để AI tự parse.

**Định nghĩa done:**
- Test cases pass cho tất cả pattern trên
- Không có false positive (parse nhầm text không phải ngày)

---

### Sprint 3 — "Make It Stable" (Tuần 4)

**Mục tiêu:** Đủ chắc để vận hành thật, có thể bàn giao cho khách sạn đầu tiên.

#### Task 3.1 — Messenger webhook signature verification `[Security]`

Verify `X-Hub-Signature-256` header trước khi xử lý bất kỳ webhook payload nào.

#### Task 3.2 — Tách full hotel context khi RAG bật `[Cost]`

Khi `RAG:Enabled = true` và `RAG:ReplaceFullHotelContext = true`:
- System prompt chỉ giữ hotel meta: tên, địa chỉ, phone, check-in/out time, chính sách cơ bản
- Bỏ toàn bộ rooms, FAQs, amenities, promotions ra khỏi system prompt
- Những thứ này được lấy qua RAG retrieval theo intent

Ước tính giảm 60-70% token trong system prompt.

#### Task 3.3 — Business logging chuẩn hóa `[Observability]`

Thêm structured log cho các event quan trọng:
- `[BookingAttempt]` bắt đầu tạo booking
- `[BookingSuccess]` booking thành công kèm bookingId
- `[BookingBlocked:Duplicate]` idempotency block
- `[BookingBlocked:PostBooking]` hậu-booking guard block
- `[LowValueMessage]` cheap-path được dùng
- `[SessionExpired]` session hết hạn, tạo mới

**Lý do:** Khi có incident ở khách sạn thật, cần trace được ngay từ log mà không cần reproduce.

---

## Những thứ KHÔNG làm trong 4 tuần này

Tôi biết team sẽ muốn làm thêm. Nhưng tôi quyết định giữ scope chặt:

- **Chưa làm:** Customer memory dài hạn (Customer profile, booking history) — cần database riêng, không phải IMemoryCache
- **Chưa làm:** Human handoff flow — phức tạp, chưa có UI cho agent
- **Chưa làm:** Booking modification / cancellation qua chatbot — làm sau khi active booking context ổn
- **Chưa làm:** API key authentication — làm song song ở backend infra, không block chatbot feature
- **Chưa làm:** Analytics dashboard — cần frontend, chưa phải ưu tiên
- **Chưa làm:** Multi-model routing thông minh (model lớn cho booking confirm, model nhỏ cho FAQ) — optimize sau khi có usage data thật

---

## Metrics theo dõi sau release

Sau khi có khách sạn đầu tiên dùng thật, theo dõi hàng ngày:

| Metric | Mục tiêu |
|---|---|
| Duplicate booking incidents | 0 |
| Hậu-booking false trigger | 0 |
| AI cost / message (average) | < $0.005 |
| Low-value message AI call rate | 0% |
| Sheets API calls / message (average) | < 2 (nhờ cache) |
| Booking conversion rate (chat → booking) | Baseline đầu tiên |
| Session timeout rate (user quay lại mất context) | Tracking để đánh giá sau Sprint 2 |

---

## Quyết định cuối

**Thứ tự làm:**

```
Sprint 1 (Tuần 1-2):
  1.1 Hậu-booking guard          ← làm đầu tiên
  1.2 Cheap-path low-value        ← làm thứ 2
  1.3 Booking idempotency         ← làm thứ 3
  1.4 Fix Random.Shared           ← làm ngay, 5 phút
  1.5 pageId → hotelId mapping    ← làm cuối Sprint 1

Sprint 2 (Tuần 3):
  2.1 3-tier memory (Active Booking Context)  ← ưu tiên cao nhất Sprint 2
  2.2 Google Sheets caching
  2.3 Temporal parser mở rộng

Sprint 3 (Tuần 4):
  3.1 Webhook signature verify
  3.2 Tách hotel context khi RAG bật
  3.3 Business logging chuẩn hóa
```

**Tiêu chí merge vào main:**
- Không break booking flow hiện tại
- Có log đủ để debug
- Không giảm chất lượng câu trả lời AI

**Definition of Done cho toàn bộ 4 tuần:**
Có ít nhất 1 khách sạn thật chạy được 1 tuần liên tục không có booking incident.
