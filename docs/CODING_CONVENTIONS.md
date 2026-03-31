# Coding Conventions

## Mục tiêu

Tài liệu này định nghĩa coding conventions cho dự án `ChatHotelAIBackend`, đặc biệt để AI coding assistant và developer mới có thể:

- hiểu đúng cách tổ chức code
- viết code nhất quán
- không làm vỡ workflow hiện tại
- ưu tiên an toàn nghiệp vụ hơn thay đổi lớn

Tài liệu này không thay thế code review. Đây là guideline mặc định để giảm sai lệch khi phát triển tiếp.

## Nguyên tắc chung

### 1. Không phá workflow đang chạy

Khi sửa code:

- ưu tiên sửa tối thiểu
- tránh refactor lớn nếu không cần thiết
- không đổi flow nghiệp vụ chỉ để làm code "đẹp hơn"
- không đổi prompt hoặc behavior AI nếu chưa có yêu cầu rõ ràng

### 2. Backend là nguồn quyết định cuối cùng

AI có thể:

- phân tích intent
- extract entity
- sinh câu trả lời tự nhiên

Nhưng backend phải là nơi quyết định:

- booking có được tạo hay không
- phòng có còn trống hay không
- giá cuối cùng là bao nhiêu
- trạng thái booking là gì

### 3. Ưu tiên state và data integrity

Nếu phải chọn giữa:

- câu trả lời nghe tự nhiên hơn
- hay nghiệp vụ an toàn hơn

thì luôn ưu tiên:

- an toàn booking
- đúng dữ liệu
- tránh duplicate

### 4. Thay đổi phải dễ rollback

Khi thêm logic mới:

- giữ phạm vi thay đổi nhỏ
- tránh chạm nhiều file không liên quan
- hạn chế side effect

## Cấu trúc dự án

### Core

Thư mục:

- `src/HotelChatbot.Core`

Chứa:

- interfaces
- DTOs
- domain models
- contracts dùng chung

Rule:

- không đặt logic hạ tầng tại đây
- không gọi HTTP, Google Sheets, OpenAI trực tiếp từ đây

### Infrastructure

Thư mục:

- `src/HotelChatbot.Infrastructure`

Chứa:

- AI providers
- Google Sheets integration
- RAG/Qdrant
- Messenger service
- business services

Rule:

- mọi integration bên ngoài nên đặt ở đây
- business logic triển khai cũng chủ yếu nằm ở đây

### API

Thư mục:

- `src/HotelChatbot.API`

Chứa:

- controllers
- startup / DI
- middleware
- HTTP endpoints

Rule:

- controller nên mỏng
- không nhét logic nghiệp vụ lớn vào controller

## Quy ước đặt tên

### Class

- PascalCase
- tên rõ vai trò

Ví dụ:

- `ConversationService`
- `BookingService`
- `MessengerWebhookService`
- `OpenAIService`

### Interface

- bắt đầu bằng `I`

Ví dụ:

- `IConversationService`
- `IBookingService`
- `IGoogleSheetsService`

### Method

- PascalCase
- async method phải có hậu tố `Async`

Ví dụ:

- `ProcessMessageAsync`
- `CreateBookingAsync`
- `GetSummaryAsync`

### Private field

- camelCase với tiền tố `_`

Ví dụ:

- `_logger`
- `_conversationService`
- `_bookingService`

### Constant

- `UPPER_SNAKE_CASE` không bắt buộc
- trong codebase hiện tại chủ yếu dùng `PascalCase` cho `const`
- hãy follow file hiện có để giữ nhất quán

## Quy ước async / await

### 1. Async all the way

- không block bằng `.Result` hoặc `.Wait()`
- nếu method gọi async downstream thì method đó cũng nên async

### 2. Hậu tố `Async`

- mọi method async public/protected nên có `Async`

### 3. Không nuốt exception vô tội vạ

- chỉ catch khi có lý do rõ ràng
- nếu catch thì phải:
  - log
  - hoặc trả fallback có chủ đích

## Logging conventions

### 1. Dùng structured logging

Ưu tiên:

```csharp
_logger.LogInformation("Booking {BookingId} created for {GuestName}", bookingId, guestName);
```

Không ưu tiên:

```csharp
_logger.LogInformation($"Booking {bookingId} created for {guestName}");
```

### 2. Log phải có ngữ cảnh

Khi log, cố gắng có:

- `sessionId`
- `hotelId`
- `bookingId`
- `senderId`
- `pageId`
- `operation`

### 3. Không log secret

Không log:

- API keys
- raw credentials
- access tokens
- sensitive config values

## Exception handling

### 1. Catch gần nơi có thể xử lý

- không catch quá sớm nếu không có action cụ thể
- service nghiệp vụ có thể catch để đổi sang message user-friendly

### 2. User-facing message phải an toàn

- không expose stack trace
- không expose credential/path nhạy cảm

### 3. Booking / transaction errors

Các lỗi booking phải ưu tiên:

- an toàn nghiệp vụ
- rõ nguyên nhân ở log
- nhẹ nhàng ở response cho user

## Controller conventions

### 1. Controller mỏng

Controller chỉ nên:

- validate request cơ bản
- gọi service
- trả response

### 2. Không để business logic lớn ở controller

Nếu logic bắt đầu có:

- branching nhiều
- query data nhiều bước
- rule nghiệp vụ

thì phải chuyển vào service.

## Service conventions

### 1. Mỗi service có trách nhiệm rõ

Ví dụ:

- `ConversationService`: orchestration hội thoại
- `BookingService`: booking nghiệp vụ
- `GoogleSheetsService`: I/O với Google Sheets
- `MessengerWebhookService`: adapter Messenger

### 2. Không trộn quá nhiều vai trò

Nếu một service vừa:

- parse webhook
- quản lý booking state
- pricing
- customer identity

thì nên tách dần khi có cơ hội hợp lý.

### 3. Orchestration service được phép gọi nhiều service

`ConversationService` là orchestration layer, nên việc gọi:

- AI
- RAG
- hotel data
- booking service

là chấp nhận được.

## DTO và model conventions

### 1. DTO dùng cho transport

- request/response giữa layer hoặc qua API
- không nhét logic hạ tầng vào DTO

### 2. Domain model phản ánh dữ liệu nghiệp vụ

- booking
- room
- pricing
- availability

### 3. Nếu thêm field mới vào session/state

Phải nghĩ đến:

- backward compatibility
- timeout / expiry
- ảnh hưởng tới flow booking hiện tại

## Quy ước với AI-related code

### 1. Không phụ thuộc hoàn toàn vào AI signal

Không dùng AI output như nguồn duy nhất cho:

- booking success
- room availability
- giá cuối cùng

### 2. AI chỉ nên hỗ trợ quyết định, không thay backend

Ví dụ:

- AI có thể nói "có vẻ khách muốn xác nhận booking"
- nhưng backend phải tự kiểm tra draft đã đủ chưa

### 3. Prompt thay đổi phải thận trọng

Khi sửa prompt:

- ghi rõ mục đích
- tránh đổi wording lớn nếu chưa test
- ưu tiên sửa logic backend hơn là vá prompt

## Quy ước với booking flow

### 1. Booking phải backend-authoritative

- booking chỉ thành công khi backend tạo record thật
- response xác nhận phải dựa trên dữ liệu backend

### 2. Tránh duplicate booking

Khi sửa phần booking:

- luôn nghĩ tới duplicate confirm
- retry
- webhook resend
- user gửi lại message

### 3. Sau booking cần nghĩ tới hậu-booking

Không xem booking thành công là hết flow.

Phải nghĩ đến:

- `cảm ơn`
- `ok`
- hỏi lại mã booking
- đổi/hủy booking
- user quay lại sau vài ngày

## Quy ước với Messenger / channel adapters

### 1. Channel adapter không quyết định nghiệp vụ lõi

Adapter nên xử lý:

- parse event
- dedupe
- debounce
- normalize message
- send reply

Không nên tự chứa logic booking phức tạp.

### 2. Webhook phải ack nhanh

- tránh chờ AI xử lý xong mới trả 200

### 3. Dùng identity nhất quán

Với Messenger, identity nên nghĩ theo:

- `pageId`
- `senderId`

không nên chỉ dựa vào một string chung chung.

## Quy ước với data source

### 1. Google Sheets là source of truth tạm thời

Khi đọc/ghi dữ liệu:

- nghĩ đến latency
- nghĩ đến retry
- nghĩ đến cache hợp lý

### 2. Đọc nhiều lần phải cân nhắc cache

Những dữ liệu ít đổi như:

- hotel info
- FAQ
- amenities

nên được cân nhắc cache.

### 3. Dữ liệu transactional phải cẩn thận hơn

Ví dụ:

- availability
- bookings

không được cache quá dài nếu sẽ làm sai nghiệp vụ.

## Quy ước với RAG

### 1. RAG cho knowledge, không cho transaction truth

RAG nên dùng cho:

- policy
- FAQ
- amenities
- room description

Không nên dùng làm nguồn thật cho:

- booking status
- availability
- final pricing

### 2. Khi RAG bật, tránh nhồi prompt dư thừa

- không duplicate cùng một khối data ở cả full context và RAG context

## Testing mindset

Khi sửa code, ít nhất phải nghĩ tới các case sau:

### Chat flow

- greeting
- low-value message
- nhiều tin nhắn liên tiếp
- user đổi ý giữa chừng

### Booking flow

- đủ dữ liệu để booking
- thiếu dữ liệu
- duplicate confirm
- room hết chỗ
- promo không hợp lệ

### Post-booking

- user nói `cảm ơn`
- user hỏi lại booking
- user quay lại sau 1-2 ngày

### Messenger

- duplicate webhook
- empty text
- echo event
- nhiều message dồn trong debounce window

## Quy ước sửa code

### 1. Ưu tiên patch nhỏ

- fix bug trước
- refactor sau

### 2. Không đổi code chỉ vì sở thích cá nhân

- nếu file đang theo pattern hiện có, hãy follow pattern đó

### 3. Không thêm abstraction không cần thiết

- chỉ thêm interface/class mới khi thực sự tăng rõ ràng:
  - testability
  - clarity
  - separation of concerns

## Những điều nên tránh

- nhét quá nhiều logic vào controller
- để AI tự quyết định nghiệp vụ booking
- dùng full prompt context cho mọi message nếu có lựa chọn tốt hơn
- sửa prompt để che một bug nghiệp vụ đáng lẽ phải fix ở backend
- dùng raw string magic quá nhiều cho state quan trọng
- để channel-specific workaround lan vào business core nếu không cần

## Definition of Good Change

Một thay đổi được xem là tốt khi:

- scope nhỏ và rõ
- không làm vỡ flow đang chạy
- log đủ để debug
- không tăng rủi ro duplicate booking
- không tăng phụ thuộc vào AI ở phần nghiệp vụ
- dễ hiểu cho người tiếp theo đọc code

## Kết luận

Convention quan trọng nhất của dự án này là:

**AI hỗ trợ hội thoại, nhưng backend phải bảo vệ nghiệp vụ.**

Nếu phải ưu tiên, luôn ưu tiên theo thứ tự:

1. an toàn booking
2. đúng dữ liệu
3. continuity của user
4. cost hợp lý
5. trải nghiệm hội thoại tự nhiên
