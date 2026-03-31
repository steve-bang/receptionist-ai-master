# TASK.md — ChatHotelAIBackend

Danh sách task triển khai theo Sprint Plan từ `docs/reports/CEO_DECISION_2026_03_31.md`.

**Trạng thái ký hiệu:**
- `[ ]` Chưa làm
- `[~]` Đang làm
- `[x]` Hoàn thành
- `[!]` Blocked / cần thảo luận

---

## Sprint 1 — Make It Safe

> **Mục tiêu:** Loại bỏ mọi rủi ro có thể gây incident booking ngay ngày đầu vận hành.
> Không release gì mới cho đến khi toàn bộ Sprint 1 xong.

---

### TASK-01 · Hậu-booking state guard `[x]`

**Priority:** P0 · **Effort:** M · **Files:** `DTOs.cs`, `ConversationService.cs`

**Bối cảnh:**
Sau booking thành công, `session.BookingDraft` vẫn còn đủ dữ liệu. Nếu khách nhắn `cảm ơn` và AI classify thành `booking_confirm` → hệ thống cố tạo booking lần 2.

**Việc cần làm:**

- [x] Thêm enum `BookingSessionStatus` vào `DTOs.cs`
  ```csharp
  public enum BookingSessionStatus { None, InProgress, Completed }
  ```
- [x] Thêm field `BookingSessionStatus BookingStatus` vào class `ConversationSession`
- [x] Thêm field `string? LastBookingId` vào `ConversationSession`
- [x] Trong `ConversationService.ProcessMessageAsync`: sau khi booking thành công
  - set `session.BookingStatus = BookingSessionStatus.Completed`
  - set `session.LastBookingId = bookingId`
  - reset `session.BookingDraft = new BookingDraftDto()`
- [x] Thêm guard ở đầu booking attempt block:
  ```csharp
  if (session.BookingStatus == BookingSessionStatus.Completed)
  {
      _logger.LogInformation("[BookingGuard] Session {SessionId} already completed, skipping booking flow", sessionId);
      shouldAttemptBooking = false;
  }
  ```

**Định nghĩa done:**
- [x] Nhắn `cảm ơn` / `ok` / `dạ` sau booking → bot trả lời tự nhiên, không tạo booking mới
- [x] Log xuất hiện dòng `[BookingGuard] Session ... already completed`
- [x] Nhắn tin hoàn toàn mới trong session mới → booking flow vẫn hoạt động bình thường

---

### TASK-02 · Cheap-path cho low-value messages

**Priority:** P0 · **Effort:** S · **Files:** `ConversationService.cs`

**Bối cảnh:**
Mọi tin nhắn kể cả `ok`, `dạ` đều tốn 2 AI calls. Tin nhắn xã giao sau booking còn có nguy cơ bị classify nhầm thành `booking_confirm`.

**Việc cần làm:**

- [ ] Thêm static field low-value patterns vào `ConversationService`:
  ```csharp
  private static readonly HashSet<string> LowValueExactPatterns = new(StringComparer.OrdinalIgnoreCase)
  {
      "ok", "okay", "dạ", "vâng", "ừ", "ừm", "uhm",
      "cảm ơn", "cảm ơn bạn", "cảm ơn em", "thank", "thanks",
      "hello", "hi", "chào", "chào em", "chào bạn",
      "?", ".", "👍", "😊", "🙏"
  };
  ```
- [ ] Thêm method `IsLowValueMessage(string message)`:
  ```csharp
  private static bool IsLowValueMessage(string message)
  {
      var normalized = message.Trim().ToLowerInvariant();
      return normalized.Length <= 20 && LowValueExactPatterns.Contains(normalized);
  }
  ```
- [ ] Thêm cheap-path block ở đầu `ProcessMessageAsync`, **trước** khi gọi `AnalyzeIntentAsync`:
  - Nếu `IsLowValueMessage` = true **và** `session.BookingStatus == Completed`:
    - Trả lời template: `"Dạ, anh/chị có cần em hỗ trợ thêm gì không ạ? 😊"`
    - Log: `[LowValueMessage][PostBooking] Skipped AI for session {SessionId}`
    - Return ngay, không gọi AI
  - Nếu `IsLowValueMessage` = true **và** session đang ở trạng thái browsing thông thường:
    - Vẫn cho qua flow nhưng log để theo dõi (chưa skip AI giai đoạn này)

**Định nghĩa done:**
- [ ] Nhắn `cảm ơn` sau booking → không có AI usage log entry nào được tạo
- [ ] Bot vẫn trả lời tự nhiên
- [ ] Log xuất hiện dòng `[LowValueMessage][PostBooking]`

---

### TASK-03 · Booking idempotency

**Priority:** P0 · **Effort:** M · **Files:** `BookingService.cs`

**Bối cảnh:**
`CreateBookingAsync` validate availability rồi tạo booking nhưng không có lock giữa 2 bước. Messenger retry hoặc webhook duplicate có thể tạo 2 booking cho cùng phòng cùng ngày.

**Việc cần làm:**

- [ ] Inject `IMemoryCache` vào `BookingService`
- [ ] Thêm method tạo idempotency key:
  ```csharp
  private static string BuildIdempotencyKey(CreateBookingRequest request)
  {
      var raw = $"{request.SessionId}:{request.RoomId}:{request.CheckInDate:yyyyMMdd}";
      var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
      return $"booking_idem_{Convert.ToHexString(bytes)[..16]}";
  }
  ```
- [ ] Đầu `CreateBookingAsync`: kiểm tra key có trong cache không
  - Nếu có → lấy bookingId đã lưu, rebuild `BookingConfirmationDto` từ Sheets, return
  - Nếu không → tiếp tục flow bình thường
- [ ] Sau khi ghi booking thành công vào Sheets: lưu `(idempotencyKey → bookingId)` vào cache, TTL 10 phút
- [ ] Log khi idempotency guard block: `[IdempotencyGuard] Duplicate blocked, returning existing {BookingId}`

**Lưu ý:**
Giải pháp này chỉ bảo vệ trong single-instance. Ghi chú TODO trong code để làm distributed lock khi scale lên multi-instance.

**Định nghĩa done:**
- [ ] Gọi `CreateBookingAsync` 2 lần liên tiếp với cùng session + room + ngày → chỉ 1 booking được tạo trong Sheets
- [ ] Log xuất hiện dòng `[IdempotencyGuard] Duplicate blocked`
- [ ] Lần 2 vẫn trả về response hợp lệ (không throw exception)

---

### TASK-04 · Fix Random.Shared

**Priority:** P0 · **Effort:** XS · **Files:** `BookingService.cs:26`

**Việc cần làm:**

- [ ] Sửa `new Random().Next(1000, 9999)` → `Random.Shared.Next(1000, 9999)`

**Định nghĩa done:**
- [ ] Không còn `new Random()` trong `BookingService`

---

### TASK-05 · pageId → hotelId mapping

**Priority:** P0 · **Effort:** S · **Files:** `MessengerOptions.cs`, `MessengerWebhookService.cs`, `appsettings.json`

**Bối cảnh:**
Mọi Facebook Page đều map về `DefaultHotelId`. Không thể phục vụ nhiều khách sạn trên nhiều Page.

**Việc cần làm:**

- [ ] Thêm property vào `MessengerOptions`:
  ```csharp
  public Dictionary<string, string> PageMappings { get; set; } = new();
  ```
- [ ] Thêm method `ResolveHotelId(string pageId)` vào `MessengerWebhookService`:
  ```csharp
  private string ResolveHotelId(string pageId)
  {
      if (_options.PageMappings.TryGetValue(pageId, out var hotelId))
          return hotelId;

      _logger.LogWarning("[MessengerMapping] No hotelId mapping for pageId={PageId}, using default", pageId);
      return _options.DefaultHotelId;
  }
  ```
- [ ] Apply trong `TrySendChatbotReplyAsync`: thay `_options.DefaultHotelId` bằng `ResolveHotelId(evt.PageId)`
- [ ] Cập nhật `appsettings.json` thêm section `PageMappings` (giá trị mẫu, không để trống)

**Định nghĩa done:**
- [ ] Page A → hotel_a, Page B → hotel_b, page không có mapping → default + log warning
- [ ] Có thể test với 2 pageId khác nhau

---

## Sprint 2 — Make It Smart

> **Mục tiêu:** Bot hiểu user đúng hơn, giảm lỗi ngày booking, hỗ trợ user quay lại sau vài giờ.

---

### TASK-06 · 3-tier memory — Active Booking Context

**Priority:** P1 · **Effort:** M · **Files:** `DTOs.cs`, `ConversationService.cs`

**Bối cảnh:**
Session hết sau 30 phút. User quay lại hỏi `booking của tôi sao rồi` → bot không biết gì. Active Booking Context tồn tại độc lập với session, expire theo checkout date.

**Việc cần làm:**

- [ ] Thêm class `ActiveBookingContext` vào `DTOs.cs`:
  ```csharp
  public class ActiveBookingContext
  {
      public string BookingId { get; set; } = "";
      public string HotelId { get; set; } = "";
      public string GuestName { get; set; } = "";
      public string GuestPhone { get; set; } = "";
      public DateTime CheckInDate { get; set; }
      public DateTime CheckOutDate { get; set; }
      public string RoomTypeName { get; set; } = "";
      public DateTime ExpiresAt { get; set; }
  }
  ```
- [ ] Sau booking thành công trong `ConversationService`:
  - Build `ActiveBookingContext` từ `BookingConfirmationDto`
  - Tính `ExpiresAt = CheckOutDate.AddDays(3)`
  - Lưu vào cache với key `booking_ctx_{hotelId}_{senderId}`, TTL = `ExpiresAt - now`
  - `senderId` ở đây là `session.SessionId` (với Messenger chính là senderId)
- [ ] Đầu `ProcessMessageAsync`: load active booking context nếu có
  - Key: `booking_ctx_{hotelId}_{sessionId}`
  - Nếu tìm thấy: inject vào system prompt thêm section `=== BOOKING ACTIVE ===`
- [ ] Nếu intent là `booking_lookup` và có active context: trả lời trực tiếp từ context, không cần AI query Sheets

**Định nghĩa done:**
- [ ] User nhắn `booking của tôi sao rồi` sau 1 giờ → bot trả lời đúng mã booking, ngày check-in
- [ ] Active context expire sau `checkout + 3 ngày`
- [ ] Session mới (sau 30 phút) vẫn load được active context nếu còn hiệu lực

---

### TASK-07 · Google Sheets caching

**Priority:** P1 · **Effort:** M · **Files:** `GoogleSheetsService.cs`

**Bối cảnh:**
Mỗi message trigger 5-6 lần gọi Sheets API. Nhiều user đồng thời → rate limit, latency cao.

**Việc cần làm:**

- [ ] Inject `IMemoryCache` vào `GoogleSheetsService`
- [ ] Wrap từng method với cache pattern, TTL theo bảng:

| Method | Cache key | TTL |
|---|---|---|
| `GetHotelInfoAsync` | `sheets_{hotelId}_hotel_info` | 15 phút |
| `GetRoomsAsync` | `sheets_{hotelId}_rooms` | 10 phút |
| `GetPricingAsync` | `sheets_{hotelId}_pricing` | 10 phút |
| `GetAmenitiesAsync` | `sheets_{hotelId}_amenities` | 30 phút |
| `GetFAQsAsync` | `sheets_{hotelId}_faqs` | 30 phút |
| `GetActivePromotionsAsync` | `sheets_{hotelId}_promotions` | 10 phút |
| `GetHolidaysAsync` | `sheets_{hotelId}_holidays` | 60 phút |
| `GetAvailabilityAsync` | `sheets_{hotelId}_avail_{checkIn}_{checkOut}` | 2 phút |

- [ ] Sau `CreateBookingAsync` và `UpdateAvailabilityAsync`: invalidate tất cả availability cache của hotel đó
  ```csharp
  // Pattern: xóa key cụ thể. Nếu không có distributed cache thì dùng short TTL (2 phút) là đủ.
  ```
- [ ] Log cache hit/miss ở mức Debug: `[SheetsCache] HIT key={key}` / `[SheetsCache] MISS key={key}`

**Định nghĩa done:**
- [ ] Nhiều message liên tiếp trong cùng 2 phút → Sheets API chỉ được gọi lần đầu
- [ ] Sau khi tạo booking → availability query tiếp theo phản ánh đúng trạng thái mới
- [ ] Không bị Sheets rate limit khi test với 5 concurrent users

---

### TASK-08 · Temporal parser mở rộng

**Priority:** P1 · **Effort:** M · **Files:** `ConversationService.cs`

**Bối cảnh:**
Parser hiện tại bỏ sót nhiều pattern tiếng Việt phổ biến → sai ngày booking.

**Việc cần làm:**

- [ ] Thêm pattern `mai`, `mốt`, `ngày kia` vào `ExtractRelativeDates`:
  ```csharp
  // Relative simple offsets
  if (Regex.IsMatch(normalized, @"\bmai\b")) results.Add(today.AddDays(1));
  if (Regex.IsMatch(normalized, @"\bmốt\b")) results.Add(today.AddDays(2));
  if (Regex.IsMatch(normalized, @"\bngày kia\b")) results.Add(today.AddDays(3));
  ```
- [ ] Thêm pattern `cuối tuần này` / `cuối tuần tới`:
  - `cuối tuần này` → Saturday tới gần nhất (nếu hôm nay đã là Sat/Sun → Saturday tuần sau)
  - `cuối tuần tới` / `cuối tuần sau` → Saturday tuần sau
- [ ] Thêm absolute date parsing: `5/4`, `05/04`, `5-4`, `5 tháng 4`, `ngày 5 tháng 4`
  - Parse ra `DateTime` với năm = năm hiện tại, nếu ngày đã qua thì +1 năm
- [ ] Thêm range parsing: `từ 5 đến 7 tháng 4` → `[5/4, 7/4]`
- [ ] Thêm duration parsing: `ở 2 đêm từ thứ 7` → check_in = thứ 7 tới, check_out = check_in + 2
- [ ] Không thay đổi thứ tự ưu tiên: parser deterministic luôn override AI entity extraction

**Test cases bắt buộc phải pass:**

| Input | check_in | check_out |
|---|---|---|
| `cho tôi đặt phòng ngày mai` | today+1 | — |
| `check in mốt` | today+2 | — |
| `cuối tuần này` | Saturday tới | — |
| `từ 5/4 đến 7/4` | 5/4/năm_hiện_tại | 7/4/năm_hiện_tại |
| `ở 2 đêm từ thứ 7 tuần tới` | thứ 7 tuần sau | thứ 7 + 2 ngày |
| `ngày 15 tháng 4` | 15/4 | — |

**Định nghĩa done:**
- [ ] Tất cả test cases trong bảng pass
- [ ] Không có false positive (parse nhầm text không phải ngày)
- [ ] Khi AI trả về date khác với parser → dùng kết quả của parser

---

## Sprint 3 — Make It Stable

> **Mục tiêu:** Đủ chắc để bàn giao cho khách sạn đầu tiên vận hành thật.

---

### TASK-09 · Messenger webhook signature verification

**Priority:** P1 · **Effort:** S · **Files:** `MessengerWebhookService.cs`, `MessengerController.cs`

**Việc cần làm:**

- [ ] Thêm method `VerifySignature(string payload, string? signatureHeader)` vào `MessengerWebhookService`:
  ```csharp
  private bool VerifySignature(string payload, string? signatureHeader)
  {
      if (string.IsNullOrEmpty(_options.AppSecret)) return true; // Skip nếu chưa config
      if (string.IsNullOrEmpty(signatureHeader)) return false;

      var expected = "sha256=" + Convert.ToHexString(
          new System.Security.Cryptography.HMACSHA256(
              System.Text.Encoding.UTF8.GetBytes(_options.AppSecret))
          .ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));

      return string.Equals(expected, signatureHeader, StringComparison.OrdinalIgnoreCase);
  }
  ```
- [ ] Trong `MessengerController`: đọc raw body trước khi deserialize, truyền vào `VerifySignature`
- [ ] Nếu signature không hợp lệ: return `403 Forbidden`, log warning

**Định nghĩa done:**
- [ ] Request không có `X-Hub-Signature-256` → reject
- [ ] Request có signature sai → reject
- [ ] Request hợp lệ → process bình thường
- [ ] `AppSecret` để trống → bỏ qua verify (backward compatible)

---

### TASK-10 · Tách full hotel context khi RAG bật

**Priority:** P1 · **Effort:** S · **Files:** `HotelAIServiceBase.cs`, `HotelDataService.cs`

**Bối cảnh:**
Khi RAG bật, system prompt hiện tại vẫn chứa full hotel context (rooms, FAQs, amenities) + thêm RAG results bên dưới → double data, tốn 60-70% token.

**Việc cần làm:**

- [ ] Thêm config `ReplaceFullHotelContext` vào `RagOptions` (đã có trong schema, verify đã đọc đúng)
- [ ] Thêm method `BuildSlimHotelContextAsync(string hotelId)` vào `IHotelDataService` và `HotelDataService`:
  - Chỉ lấy: tên khách sạn, địa chỉ, phone, email, check-in time, check-out time, chính sách cơ bản
  - Không lấy: rooms, amenities, FAQs, promotions
- [ ] Trong `HotelAIServiceBase.BuildSystemPromptAsync`: nếu `RAG:Enabled = true` và `RAG:ReplaceFullHotelContext = true` → dùng `BuildSlimHotelContextAsync` thay vì `BuildHotelContextAsync`
- [ ] Update `appsettings.json`: set `"ReplaceFullHotelContext": true`

**Định nghĩa done:**
- [ ] Khi RAG bật: system prompt không chứa danh sách phòng, FAQ, amenities
- [ ] AI usage log cho thấy `SystemPromptChars` giảm rõ rệt
- [ ] Bot vẫn trả lời đúng câu hỏi về phòng/FAQ (nhờ RAG retrieval)

---

### TASK-11 · Business logging chuẩn hóa

**Priority:** P1 · **Effort:** S · **Files:** `ConversationService.cs`, `BookingService.cs`

**Bối cảnh:**
Khi có incident ở khách sạn thật, cần trace được ngay từ log mà không cần reproduce.

**Việc cần làm:**

Thêm structured log cho các event sau:

- [ ] `[BookingAttempt]` — khi bắt đầu cố tạo booking
  ```
  [BookingAttempt] sessionId={id} hotelId={hotel} roomId={room} checkIn={date} guestPhone={phone}
  ```
- [ ] `[BookingSuccess]` — khi booking được tạo thành công
  ```
  [BookingSuccess] bookingId={id} sessionId={id} hotelId={hotel} roomId={room} guestPhone={phone}
  ```
- [ ] `[BookingBlocked:Duplicate]` — idempotency guard block
- [ ] `[BookingBlocked:PostBooking]` — hậu-booking guard block
- [ ] `[BookingFailed]` — booking thất bại (kèm exception message)
- [ ] `[LowValueMessage]` — cheap-path được dùng
- [ ] `[ActiveContextLoaded]` — load được active booking context cho session

**Định nghĩa done:**
- [ ] Có thể trace toàn bộ lifecycle một booking chỉ bằng `grep bookingId log.txt`
- [ ] Không có sensitive info (full phone, tên đầy đủ) trong log — chỉ dùng partial (4 số cuối phone)

---

## Backlog — Chưa làm trong 3 Sprint này

Những task dưới đây đã được ghi nhận nhưng sẽ làm sau khi 3 sprint trên hoàn thành. Không làm sớm hơn kế hoạch.

| # | Task | Lý do hoãn |
|---|---|---|
| B-01 | Customer memory dài hạn (profile, booking history) | Cần database riêng, không phải IMemoryCache |
| B-02 | API key authentication cho `/api/chat/message` | Làm ở infra layer, không block chatbot |
| B-03 | Rate limiting | Làm song song với auth |
| B-04 | Booking lookup / cancellation qua chatbot | Cần active booking context (TASK-06) xong trước |
| B-05 | CORS restrict theo domain thật | Cần biết domain production |
| B-06 | Hoàn thiện RAG retrieval quality | Cần có usage data thật để tune |
| B-07 | Human handoff flow | Cần UI cho agent, chưa có |
| B-08 | Booking modification qua chatbot | Complex, không phải MVP |
| B-09 | Analytics dashboard | Cần frontend |
| B-10 | Multi-model routing (model nhỏ cho FAQ, model lớn cho booking) | Optimize sau khi có cost data thật |

---

## Definition of Done — Toàn bộ project

Sản phẩm sẵn sàng bàn giao khách sạn đầu tiên khi:

- [ ] TASK-01: Không tạo duplicate booking sau confirm
- [ ] TASK-02: `cảm ơn`, `ok`, `dạ` sau booking không trigger booking flow và không tốn AI call
- [ ] TASK-03: 2 request đồng thời không tạo 2 booking
- [ ] TASK-04: Không còn `new Random()` trong BookingService
- [ ] TASK-05: Messenger page map đúng hotel
- [ ] TASK-06: User quay lại sau 1 giờ vẫn được hỗ trợ đúng booking active
- [ ] TASK-07: Không bị Sheets rate limit khi test 5 concurrent users
- [ ] TASK-08: Các pattern ngày tiếng Việt phổ biến parse đúng
- [ ] TASK-09: Fake webhook bị reject
- [ ] TASK-10: System prompt giảm rõ rệt khi RAG bật
- [ ] TASK-11: Có thể trace lifecycle booking từ log

**Tiêu chí cuối cùng:** 1 khách sạn thật chạy liên tục 1 tuần không có booking incident.
