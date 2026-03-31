# Changelog

Tài liệu này ghi lại các thay đổi chính của dự án theo hướng thực dụng, tập trung vào những mốc có ảnh hưởng tới sản phẩm, kiến trúc và vận hành.

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
  - hậu-booking state guard
  - idempotency cho booking
  - active booking context
  - customer memory dài hạn
  - `pageId -> hotelId` mapping khi scale multi-hotel
