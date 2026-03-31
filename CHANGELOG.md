# Changelog

Tài liệu này ghi lại các thay đổi chính của dự án theo hướng thực dụng, tập trung vào những mốc có ảnh hưởng tới sản phẩm, kiến trúc và vận hành.

## [Phase 1 — Multi-Hotel SaaS Foundation] · 2026-03-31

### Added

- **Zalo OA channel** — Tích hợp Zalo Official Account như một channel chatbot độc lập:
  - `ZaloOptions` với `OaHotelMapping`, `OaAccessTokenMapping`, `OaSecretKeyMapping`
  - `ZaloWebhookService` với debounce 5s, webhook signature verify (HMAC-SHA256)
  - `IZaloWebhookService` interface trong `IServices.cs`
  - `ZaloController` tại `POST /api/zalo/webhook`
  - Session key isolate theo `zalo:{oaId}:{userId}`
  - _Files: `ZaloOptions.cs`, `ZaloWebhookService.cs`, `ZaloController.cs`, `IServices.cs`, `Program.cs`_

### Changed

- **Google Sheets per-hotel routing** — Mỗi hotel có thể có Google Spreadsheet riêng:
  - Thêm `HotelSpreadsheetMapping` vào `GoogleSheetsOptions`
  - `ResolveSpreadsheetId(hotelId)` — lookup hotel spreadsheet, fallback về platform SpreadsheetId
  - Tất cả read/write operations dùng đúng spreadsheet của hotel
  - AI usage logs vẫn ghi vào platform SpreadsheetId (cross-hotel reporting)
  - `GetBookingAsync` và `CancelBookingAsync` tìm kiếm qua tất cả spreadsheets được cấu hình
  - _Files: `GoogleSheetsService.cs`_

- **Messenger per-page access token** — Mỗi Facebook Page dùng access token riêng khi gửi reply:
  - Thêm `PageAccessTokenMapping` vào `MessengerOptions`
  - `ResolvePageAccessToken(pageId)` — lookup token per page, fallback về `PageAccessToken` mặc định
  - _Files: `MessengerOptions.cs`, `MessengerWebhookService.cs`_

- **appsettings.json** — Thêm config sections cho `Zalo`, `GoogleSheets.HotelSpreadsheetMapping`, `Messenger.PageAccessTokenMapping`

### Notes

- Zalo access token hết hạn sau 3 giờ — Phase 1 cần refresh thủ công qua Zalo OA portal. Auto-refresh là Phase 2.
- CORS vẫn đang AllowAll — cần restrict trước production.

---

## [Sprint 1 — Make It Safe] · 2026-03-31

### Fixed

- **[TASK-01] Hậu-booking state guard** — Sau khi booking thành công, session không thể tạo thêm booking thứ 2 dù user nhắn `cảm ơn`, `ok`, hay bất kỳ tin nhắn nào khác trong cùng session.
  - Thêm `BookingSessionStatus` enum (`None / InProgress / Completed`) vào `ConversationSession`
  - Thêm field `LastBookingId` để trace booking đã tạo
  - Guard block trước khi attempt booking: nếu `BookingStatus == Completed` thì skip, log `[BookingGuard]`
  - Set `BookingStatus = Completed` + reset `BookingDraft` ngay sau khi booking tạo thành công
  - _Files: `DTOs.cs`, `ConversationService.cs`_

- **[TASK-02] Cheap-path cho low-value messages** — Tin nhắn xã giao sau booking (`cảm ơn`, `ok`, `dạ`, `hello`, v.v.) không còn tốn 2 AI calls nữa.
  - Thêm `LowValuePatterns[]` với 16 patterns phổ biến
  - Thêm `IsLowValueMessage()` rule-based (length < 20 + pattern match)
  - Cheap-path early return ngay đầu `ProcessMessageAsync`: nếu `BookingCompleted` && low-value → trả template, skip hoàn toàn `AnalyzeIntentAsync` + `GetChatCompletionAsync`
  - _Files: `ConversationService.cs`_

### Changed

- **Booking draft context trong system prompt** — `BookingDraft` hiện được inject vào system prompt dưới dạng "GROUND TRUTH" khi có data. AI bắt buộc đọc đúng ngày check-in/out, số đêm, tên khách từ draft thay vì tự suy từ conversation history — khắc phục tình trạng AI báo sai ngày/giá trong booking summary.
  - Thêm `BuildBookingDraftContext()` method
  - _Files: `ConversationService.cs`_

---

## Unreleased

### Added

- Tích hợp provider AI theo config, hỗ trợ chuyển đổi giữa OpenAI và Claude.
- Bổ sung OpenAI chat provider cho flow chatbot.
- Scaffold RAG với Qdrant, embedding service, indexing flow và admin endpoints cơ bản.
- Thêm AI usage tracking:
  - prompt tokens
  - completion tokens
  - total tokens
  - estimated cost USD/VND
- Thêm `AiUsageLogs` và summary endpoint để theo dõi cost vận hành.
- Thêm Messenger webhook integration:
  - verify webhook
  - nhận message
  - gửi reply lại qua Messenger Send API
- Thêm debounce `5s` cho Messenger để gom nhiều tin nhắn liên tiếp trước khi gọi chatbot.
- Thêm tài liệu sản phẩm và vận hành trong thư mục `docs/`, bao gồm:
  - API documentation
  - Google Sheets guide
  - Facebook Messenger setup
  - RAG design / setup
  - GPT cost analysis
  - release gap analysis
  - production blueprint
  - implementation roadmap
  - PRD
  - go-to-market
  - demo script
  - vision & mission

### Changed

- Điều chỉnh flow booking để backend là nguồn quyết định cuối cùng cho booking thành công.
- Booking confirmation chỉ được trả về từ dữ liệu backend sau khi tạo booking thật.
- Bổ sung xử lý ngày tương đối tiếng Việt tốt hơn trong flow extraction, ví dụ:
  - `thứ 7 tuần tới`
  - `thứ 2 tuần tới`
  - `ngày 13 tháng tới`
- Cải thiện Messenger adapter để:
  - bỏ qua echo events
  - bỏ qua message rỗng
  - chống duplicate theo message id và fingerprint text
  - trả `200` nhanh hơn ở webhook

### Fixed

- Sửa các lỗi build blocker trong model/domain mapping.
- Sửa lỗi payload không hợp lệ khi tích hợp OpenAI.
- Sửa lỗi chatbot có thể tuyên bố booking thành công trước khi backend tạo booking thật.
- Sửa lỗi duplicate reply trên Messenger do webhook xử lý chậm và event bị gửi lại.
- Ngừng track các thư mục build artifact `bin/Debug` trên git.

### Infrastructure

- Hoàn thiện `.gitignore` cho:
  - `.NET bin/obj`
  - IDE files
  - logs
  - local temp files
  - local config patterns

### Notes

- Một số hạng mục production-hardening vẫn đang nằm trong backlog, đặc biệt:
  - idempotency cho booking (TASK-03)
  - fix `new Random()` mỗi lần — booking ID có thể trùng (TASK-04)
  - `pageId -> hotelId` mapping khi scale multi-hotel (TASK-05)
  - active booking context
  - customer memory dài hạn
