# Release Gap Analysis

## Mục tiêu

Tài liệu này tổng hợp khoảng cách giữa trạng thái hiện tại của hệ thống chatbot và trạng thái nên có để vận hành production ổn định cho use case khách sạn.

Phạm vi tập trung:

- Chat flow
- Booking flow
- Messenger channel
- Session/context
- Customer memory
- Vận hành và an toàn release

## Tóm tắt nhanh

Hệ thống hiện tại đã có nền tảng tốt:

- Chatbot WebAPI chạy được
- Booking có thể ghi vào Google Sheets
- Có usage tracking cho GPT
- Có Messenger webhook receive + reply
- Có debounce 5 giây cho message liên tiếp
- Có scaffold RAG/Qdrant

Tuy nhiên, còn một số khoảng trống quan trọng nếu muốn release an toàn:

- Chưa có state hậu-booking đủ rõ
- Chưa có active booking context theo vòng đời booking
- Chưa có customer memory dài hạn
- Chưa có idempotency mạnh ở tầng booking
- Chưa có cheap-path cho low-value messages
- Chưa hoàn chỉnh multi-page / multi-hotel routing

## Kiến trúc hiện tại

### Những gì đang hoạt động

- User gửi message vào API hoặc Messenger webhook
- `ConversationService` phân tích intent, build context, gọi AI
- Nếu đủ điều kiện xác nhận booking, `BookingService` tạo booking và ghi lên Google Sheets
- Messenger adapter nhận webhook và chuyển text vào chatbot service
- GPT usage được log và lưu vào `AiUsageLogs`

### Những gì đang là điểm yếu

- Session đang dựa vào `IMemoryCache`
- Session timeout ngắn, không gắn với lifecycle booking
- Sau booking thành công, ngữ cảnh cũ vẫn có thể ảnh hưởng message tiếp theo
- Long-term memory của user chưa tách riêng khỏi short-term chat session

## So sánh hiện trạng và trạng thái mong muốn

| Chủ đề | Hiện trạng | Trạng thái mong muốn |
|---|---|---|
| Session chat | Session ngắn hạn bằng memory cache | Session ngắn hạn + state theo booking lifecycle |
| Hậu-booking | Chưa có nhánh riêng | Có flow riêng cho cảm ơn, hỏi mã booking, đổi/hủy booking |
| Customer memory | Gần như chưa có | Có customer profile + booking history |
| Booking safety | Có validate availability | Có thêm idempotency và anti-duplicate booking |
| Messenger | Nhận webhook, reply, debounce | Có thêm identity mapping và channel routing chắc chắn |
| Multi-hotel | Chưa hoàn chỉnh | Map `pageId -> hotelId` rõ ràng |
| Low-value messages | Vẫn có thể đi full AI flow | Có cheap-path rule-based |
| Temporal parsing | Mới hỗ trợ một phần | Bao phủ nhiều pattern tiếng Việt hơn |

## Nhóm vấn đề chính

### 1. Hậu-booking flow

#### Hiện tại

- Sau khi booking thành công, user vẫn ở cùng session
- Message tiếp theo vẫn đi full AI flow
- Nếu AI classify lệch sang `booking_confirm`, có thể gọi lại nhánh tạo booking

#### Rủi ro

- Duplicate booking attempt
- Trả lời lỗi khó hiểu cho các tin nhắn như `cảm ơn`, `ok`
- Trải nghiệm hậu-booking thiếu tự nhiên

#### Nên có

- Một trạng thái rõ ràng kiểu `booking_completed`
- Một active booking context gắn với booking gần nhất
- Message dạng acknowledgement không được kích hoạt nhánh booking

### 2. Session và context lifecycle

#### Hiện tại

- Session tồn tại ngắn hạn
- Sau 1-2 ngày user quay lại thường là session mới hoàn toàn

#### Rủi ro

- Không nhớ booking gần nhất của user
- Không hỗ trợ tốt các câu như:
  - `booking của tôi sao rồi`
  - `tôi muốn đổi ngày`
  - `mã đặt phòng là gì`

#### Nên có

- Session ngắn hạn cho hội thoại hiện tại
- Active booking context sống đến `check_out_date + grace period`
- Hết active context thì tạo session mới, nhưng vẫn tra được booking history

### 3. Customer memory dài hạn

#### Hiện tại

- Chưa có lớp customer profile dài hạn
- Chưa có cách chuẩn để nối user quay lại với booking cũ

#### Rủi ro

- Bot không biết user đang hỏi về booking nào
- Dễ trả lời chung chung hoặc sai ngữ cảnh

#### Nên có

- Lưu tối thiểu:
  - `senderId`
  - `pageId`
  - `phone`
  - `name`
  - `lastBookingId`
  - `booking history`

### 4. Booking idempotency và an toàn giao dịch

#### Hiện tại

- `BookingService` có validate availability
- Nhưng chưa có idempotency key rõ ràng cho một confirmation event

#### Rủi ro

- Duplicate attempt nếu:
  - AI confirm lại
  - webhook retry
  - user gửi lại xác nhận
  - channel resend

#### Nên có

- Một cơ chế xác định:
  - cùng một confirmation chỉ được tạo booking một lần
- Có thể theo:
  - `sessionId + draft hash`
  - hoặc `confirmation token`

### 5. Low-value messages

#### Hiện tại

- Các tin như `ok`, `dạ`, `vâng`, `cảm ơn`, `hello`, `?` vẫn có thể chạy full AI flow

#### Rủi ro

- Tốn token
- Dễ classify sai intent
- Hậu-booking càng dễ phát sinh hành vi không mong muốn

#### Nên có

- Cheap-path cho:
  - greeting
  - thanks
  - acknowledgement
  - yes/no ngắn

### 6. Temporal language

#### Hiện tại

- Đã tốt hơn với:
  - `thứ 7 tuần tới`
  - `thứ 2 tuần tới`
  - `ngày 13 tháng tới`

#### Còn thiếu

- `mai`, `mốt`, `ngày kia`
- `cuối tuần này`
- `đầu tháng sau`
- `2 đêm từ thứ 7 tuần tới`
- `ở từ 5 đến 7`
- `5/4`, `05-04`, `5 tháng 4`

#### Nên có

- Parser deterministic mạnh hơn cho tiếng Việt thời gian

### 7. Messenger channel completeness

#### Hiện tại

- Nhận webhook
- Reply được
- Có debounce 5 giây

#### Còn thiếu

- `pageId -> hotelId` mapping chuẩn
- identity mapping theo page/user
- attachment/image/voice handling
- human handoff rules
- signature verification cứng nếu muốn bảo mật hơn

### 8. Multi-hotel / multi-page

#### Hiện tại

- Vẫn còn theo hướng một cấu hình tương đối đơn giản

#### Rủi ro

- Lấy sai dữ liệu khách sạn
- Lẫn session giữa các page nếu scale không chặt

#### Nên có

- `pageId -> hotelId`
- `pageId + senderId` là conversation identity ở tầng channel
- booking context cũng phải theo hotel

## Must Fix Before Release

### Nhóm bắt buộc nên xử lý trước release

1. Hậu-booking state
2. Chặn duplicate booking sau khi đã confirm
3. Cheap-path cho `cảm ơn`, `ok`, `dạ`, greeting
4. Active booking context theo vòng đời booking
5. Identity mapping đủ để user quay lại vẫn được hỗ trợ đúng booking

## Should Fix Soon After Release

1. Mở rộng temporal parser tiếng Việt
2. `pageId -> hotelId` đầy đủ cho nhiều khách sạn
3. Hỗ trợ tra booking theo số điện thoại / booking id
4. Handoff cho human support
5. Hoàn thiện policy hậu-booking: đổi ngày, hủy, chỉnh số khách

## Nice to Have

1. Attachment understanding
2. Typing indicator / channel UX tốt hơn
3. Message summarization cho long-term memory
4. Analytics sâu hơn theo funnel booking
5. Intent confidence guard và auto-fallback rule engine

## Đề xuất mô hình trạng thái đúng hơn

### Tầng 1: Short-term conversation session

- Giữ context hội thoại gần
- Expire nhanh
- Dùng cho chat đang diễn ra

### Tầng 2: Active booking context

- Gắn với booking đang hiệu lực
- Expire theo `check_out_date + grace period`
- Dùng cho:
  - hậu-booking
  - hỏi mã booking
  - hỏi check-in
  - đổi/hủy booking

### Tầng 3: Customer memory dài hạn

- Gắn với user profile
- Không nhất thiết lưu raw message full để feed lại AI
- Quan trọng nhất là:
  - customer identity
  - booking history
  - booking gần nhất

## Chiến lược xử lý thông minh hơn

### Không nên

- Feed toàn bộ lịch sử chat cũ vào AI mỗi lần user quay lại

### Nên

- Dùng session gần cho hội thoại hiện tại
- Dùng booking record làm source of truth
- Dùng customer memory để tra context phù hợp
- Chỉ tóm tắt lịch sử khi thật sự cần

## Kết luận

Nếu nhìn theo readiness để release, vấn đề lớn nhất hiện nay không còn là chuyện gọi model hay thiếu data cơ bản, mà là quản lý state nghiệp vụ quanh booking và user lifecycle.

Tóm lại, hệ thống nên tiến theo hướng:

- Chat session ngắn hạn
- Active booking context theo vòng đời booking
- Customer memory dài hạn
- Guard rõ ràng cho hậu-booking và duplicate booking
- Cheap-path cho tin nhắn ngắn, xã giao

Đây là những bước sẽ tạo ra khác biệt lớn nhất về độ ổn định khi vận hành thật.
