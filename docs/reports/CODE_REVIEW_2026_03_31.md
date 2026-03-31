# Code Review Report

**Ngày:** 2026-03-31
**Scope:** Toàn bộ source code `ChatHotelAIBackend`
**Reviewer:** Claude AI

---

## Tổng quan

Codebase có kiến trúc 3 lớp sạch (Core → Infrastructure → API), dùng interface và DI đúng cách. Nền tảng tốt để mở rộng.

Tuy nhiên có một số vấn đề cần giải quyết trước khi vận hành production thật — đặc biệt là **state management quanh booking**, không phải phần AI.

---

## Điểm tốt hiện có

- Kiến trúc 3 lớp rõ ràng: `HotelChatbot.Core`, `HotelChatbot.Infrastructure`, `HotelChatbot.API`
- Interface-first design, DI container đầy đủ, dễ swap implementation
- `MessengerWebhookService` viết chắc: debounce 5 giây, merge message liên tiếp, chống duplicate bằng messageId cache + text fingerprint
- RAG scaffold đúng thiết kế: toggle được bằng config `RAG:Enabled`, có `NoOpRagContextService` fallback
- AI usage tracking chi tiết: prompt tokens, completion tokens, cost USD/VND, breakdown theo operation/hotel/session
- Temporal parser có baseline: xử lý được `thứ X tuần tới/sau/này`, `ngày X tháng tới`
- Serilog logging có cấu trúc, ghi ra file và console
- Health check endpoint `/health`
- Multi AI provider: có thể swap giữa OpenAI và Claude qua config `AIProvider:Provider`

---

## Vấn đề P0 — Phải sửa trước khi cho khách dùng thật

### 1. Không có hậu-booking state guard

**File:** `ConversationService.cs:132`

```csharp
var shouldAttemptBooking = isBookingReady || intent.Intent == "booking_confirm";
```

**Vấn đề:**
Sau khi booking thành công, `session.BookingDraft` vẫn còn đủ dữ liệu (tên, phone, ngày, phòng). Nếu khách nhắn `cảm ơn` hoặc `ok` và AI classify thành `booking_confirm`, hoặc AI vô tình thêm `[BOOKING_READY]` → hệ thống cố tạo booking lần 2.

`ConversationSession` hiện tại không có field nào đánh dấu "booking đã hoàn tất".

**Rủi ro:** Duplicate booking với cùng khách, cùng phòng, cùng ngày.

**Hướng sửa:**
- Thêm `BookingStatus` enum vào `ConversationSession`: `None`, `InProgress`, `Completed`
- Sau booking thành công: set `session.BookingStatus = Completed`, clear `BookingDraft`
- Guard đầu `ProcessMessageAsync`: nếu `BookingStatus == Completed` thì skip booking flow

---

### 2. Không có idempotency ở tầng booking

**File:** `BookingService.cs:50-108`

**Vấn đề:**
`CreateBookingAsync` validate availability rồi tạo booking, nhưng không có lock giữa 2 bước. Nếu 2 request đồng thời (Messenger retry, webhook duplicate vượt qua cache):

```
Request A: validate → available ✓
Request B: validate → available ✓  (A chưa ghi xong)
Request A: ghi booking → thành công
Request B: ghi booking → thành công (double booking!)
```

**Rủi ro:** 2 booking cho cùng phòng, cùng ngày.

**Hướng sửa:**
- Thêm idempotency key: `sessionId + checkInDate + roomId` → hash
- Cache key trong `IMemoryCache` trước khi tạo booking, TTL 5 phút
- Nếu key đã tồn tại → trả về booking cũ, không tạo mới

---

### 3. Mọi message đều tốn 2 AI calls

**File:** `HotelAIServiceBase.cs:76`, `ConversationService.cs:92`

**Vấn đề:**
Với mọi tin nhắn (kể cả `ok`, `dạ`, `cảm ơn`, `hello`):
1. `AnalyzeIntentAsync` → gọi AI lần 1 (intent analysis)
2. `GetChatCompletionAsync` → gọi AI lần 2 (chat reply)

= 2 AI calls × cost/call × tất cả low-value messages

**Rủi ro:** Tốn token không cần thiết, tăng cost, delay phản hồi, và quan trọng nhất — AI có thể classify sai `booking_confirm` cho các tin nhắn xã giao.

**Hướng sửa:**
Thêm cheap-path rule-based đầu `ProcessMessageAsync`:

```csharp
private static readonly string[] LowValuePatterns =
    ["ok", "dạ", "vâng", "cảm ơn", "thanks", "hello", "hi", "chào", "?"];

private bool IsLowValueMessage(string message)
{
    var normalized = message.Trim().ToLowerInvariant();
    return normalized.Length < 20
        && LowValuePatterns.Any(p => normalized == p || normalized.Contains(p));
}
```

Nếu là low-value và session đang ở `BookingCompleted` → trả lời template, skip AI hoàn toàn.

---

### 4. Thiếu `pageId → hotelId` mapping

**File:** `MessengerWebhookService.cs:216`

```csharp
HotelId = _options.DefaultHotelId,
```

**Vấn đề:**
Mọi Facebook Page đều map về cùng 1 hotel. Nếu một hệ thống phục vụ 2 khách sạn trên 2 Page khác nhau → cả 2 đều nhận data của hotel "default".

**Rủi ro:**
- Sai dữ liệu phòng/giá khi multi-hotel
- Booking được ghi vào sai hotel
- Không thể scale SaaS

**Hướng sửa:**
Thêm dictionary vào `MessengerOptions`:

```json
"Messenger": {
  "PageMappings": {
    "1234567890": "hotel_abc",
    "0987654321": "hotel_xyz"
  },
  "DefaultHotelId": "default"
}
```

---

## Vấn đề P1 — Nên sửa sớm sau release

### 5. Không có caching cho Google Sheets

**File:** `HotelDataService.cs`, `GoogleSheetsService.cs`

**Vấn đề:**
`GoogleSheetsService` là Singleton nhưng không cache data. Mỗi message có thể trigger 5-6 lần gọi API Sheets:
- `GetRoomsAsync`
- `GetPricingAsync`
- `GetAmenitiesAsync`
- `GetFAQsAsync`
- `GetHolidaysAsync`
- `GetActivePromotionsAsync`

Với nhiều user đồng thời → bị Google Sheets rate limit, latency cao.

**Hướng sửa:**
Cache tại `GoogleSheetsService` dùng `IMemoryCache`:
- Hotel info: 15 phút
- Rooms, pricing: 10 phút
- FAQ, amenities: 30 phút
- Availability: 2 phút (thay đổi nhanh sau booking)

---

### 6. Temporal parser còn thiếu nhiều pattern tiếng Việt

**File:** `ConversationService.cs:238-262`

Hiện tại chỉ handle:
- `thứ X tuần tới/sau/này`
- `ngày X tháng tới/sau/này`

Còn thiếu:
- `mai` → today + 1
- `mốt` → today + 2
- `ngày kia` → today + 3
- `cuối tuần này` → Saturday tới
- `5/4`, `05-04`, `5 tháng 4` → absolute date parsing
- `ở 2 đêm từ thứ 7` → check_in + duration → check_out
- `từ 5 đến 7 tháng 4` → range parsing

**Rủi ro:** Sai ngày booking — lỗi nghiêm trọng về nghiệp vụ.

---

### 7. `BuildHotelContextAsync` nhồi toàn bộ data vào system prompt mỗi lần

**File:** `HotelAIServiceBase.cs:17-18`, `HotelDataService.cs:159`

**Vấn đề:**
Mỗi message → `BuildHotelContextAsync` build 1 string khổng lồ gồm toàn bộ phòng + tiện ích + FAQ + promotions → nhét vào system prompt. Khi RAG bật, còn thêm RAG context bên dưới → **double data trong prompt**.

Ví dụ với 10 phòng × mô tả dài + 20 FAQ + tiện ích = dễ vượt 4000-6000 tokens chỉ riêng system prompt.

**Hướng sửa:**
- Khi `RAG:Enabled = true` → dùng RAG context thay cho full hotel context, chỉ giữ lại hotel meta (tên, địa chỉ, phone, check-in/out time)
- Khi `RAG:Enabled = false` → giữ nguyên như hiện tại

---

### 8. Không có Active Booking Context

**File:** `DTOs.cs` (ConversationSession)

**Vấn đề:**
Session hết hạn sau 30 phút. User quay lại sau 1 giờ → session mới, không biết user đang có booking active nào. Bot không thể:
- Trả lời `booking của tôi sao rồi`
- Nhắc lại mã booking
- Hỗ trợ hậu-booking

**Hướng sửa:**
Thêm `ActiveBookingContext` vào `ConversationSession`:

```csharp
public class ActiveBookingContext
{
    public string BookingId { get; set; } = "";
    public string HotelId { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public DateTime ExpiresAt { get; set; } // CheckOutDate + 3 days
}
```

Lưu riêng với key `booking_context_{senderId}`, expire theo `CheckOutDate + grace period`.

---

### 9. `[BOOKING_READY]` marker approach không đáng tin cậy

**File:** `HotelAIServiceBase.cs:72`, `ConversationService.cs:127`

**Vấn đề:**
AI được yêu cầu kết thúc response bằng `[BOOKING_READY]` khi booking hoàn tất. Nhưng:
- AI có thể thêm marker này khi đang giải thích quy trình booking
- AI có thể không thêm marker khi cần
- Không có nghiệp vụ nào kiểm soát marker này

**Hướng sửa:**
Thay bằng state machine rõ ràng:
- `ConversationService` tự quyết định lúc nào attempt booking dựa vào `IsBookingDraftComplete()`
- Không phụ thuộc AI signal
- AI chỉ làm nhiệm vụ tóm tắt và hỏi xác nhận

---

### 10. `new Random()` mỗi lần trong GenerateBookingId

**File:** `BookingService.cs:26-28`

```csharp
var random = new Random().Next(1000, 9999);
```

**Vấn đề:** `new Random()` không seed đủ tốt khi gọi nhiều lần nhanh → có thể sinh ra cùng số → booking ID trùng.

**Sửa ngay:**
```csharp
var random = Random.Shared.Next(1000, 9999);
```

---

## Vấn đề bảo mật cần fix trước production

### 11. Không có authentication trên API endpoints

Không có API key, không có JWT, không có rate limiting trên:
- `POST /api/chat/message`
- `POST /api/booking`
- `POST /api/rag/reindex/{hotelId}`

Ai cũng có thể gọi tùy ý.

### 12. Không verify Messenger webhook signature

`MessengerWebhookService` không validate header `X-Hub-Signature-256`. Ai cũng có thể fake webhook payload để spam chatbot hoặc trigger booking.

### 13. CORS AllowAll cần restrict trước production

**File:** `Program.cs:106-114`

```csharp
policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
```

Cần restrict về domain thật của frontend khi deploy.

---

## Danh sách công việc theo ưu tiên

### P0 — Phải làm trước khi cho khách dùng thật

| # | Việc cần làm | File chính |
|---|---|---|
| 1 | Thêm `BookingCompletedStatus` vào session, guard hậu-booking | `ConversationService.cs`, `DTOs.cs` |
| 2 | Cheap-path rule-based cho low-value messages — skip AI | `ConversationService.cs` |
| 3 | Idempotency key cho booking creation | `BookingService.cs` |
| 4 | `pageId → hotelId` mapping trong `MessengerOptions` | `MessengerWebhookService.cs`, `MessengerOptions.cs` |
| 5 | Fix `new Random()` → `Random.Shared` | `BookingService.cs` |

### P1 — Sớm sau release

| # | Việc cần làm | File chính |
|---|---|---|
| 6 | Cache Google Sheets data (hotel info 15p, rooms 10p, FAQ 30p, availability 2p) | `GoogleSheetsService.cs` |
| 7 | Mở rộng temporal parser: `mai`, `mốt`, `cuối tuần`, `DD/MM`, range | `ConversationService.cs` |
| 8 | Tách full hotel context ra khỏi prompt khi RAG bật | `HotelAIServiceBase.cs` |
| 9 | Active Booking Context tồn tại qua session timeout | `DTOs.cs`, `ConversationService.cs` |
| 10 | Verify Messenger `X-Hub-Signature-256` | `MessengerWebhookService.cs` |
| 11 | Thay `[BOOKING_READY]` marker bằng state machine | `ConversationService.cs`, `HotelAIServiceBase.cs` |

### P2 — Khi scale lên nhiều khách sạn

| # | Việc cần làm |
|---|---|
| 12 | Customer memory dài hạn: `senderId/pageId → customerId`, booking history |
| 13 | API key authentication cho `/api/chat/message` và admin endpoints |
| 14 | Rate limiting cho API và Messenger webhook |
| 15 | Booking lookup qua chatbot: hỏi mã booking, check-in info |
| 16 | CORS restrict theo domain thật |
| 17 | Hoàn thiện RAG: bật retrieval thật cho FAQ/policies |
| 18 | Handoff flow: detect khiếu nại, route sang human |
| 19 | Usage dashboard endpoint chi tiết hơn |

---

## Định nghĩa "Done" trước production

Bản build được xem là sẵn sàng khi:

- [ ] Không tạo duplicate booking sau khi đã confirm
- [ ] Tin nhắn `cảm ơn`, `ok`, `dạ` sau booking không trigger booking flow
- [ ] Low-value messages không tốn AI call
- [ ] Messenger page đúng hotel khi có nhiều page
- [ ] Có đủ log để debug incident booking
- [ ] Booking ID không bị trùng
- [ ] Cost AI đo được theo hotel

---

## Kết luận

Vấn đề lớn nhất hiện tại **không phải phần AI** mà là **state management quanh booking**:

1. Hậu-booking guard
2. Idempotency
3. Cheap-path cho low-value messages

Ba vấn đề này ảnh hưởng trực tiếp đến tính đúng đắn của nghiệp vụ và cost vận hành. Nên giải quyết trước khi cho khách sạn thật sử dụng.

Sau khi xong P0, ưu tiên tiếp theo là **Google Sheets caching** (giảm latency + tránh rate limit) và **temporal parser mở rộng** (giảm sai ngày booking).
