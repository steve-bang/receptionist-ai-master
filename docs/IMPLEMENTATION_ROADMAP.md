# Implementation Roadmap

## Mục tiêu

Tài liệu này chuyển blueprint kiến trúc thành kế hoạch triển khai thực tế theo từng phase để đội phát triển có thể thực hiện dần, giảm rủi ro và ưu tiên đúng các hạng mục quan trọng trước release.

Nguyên tắc lập roadmap:

- ưu tiên an toàn nghiệp vụ trước
- không làm vỡ workflow hiện tại
- mỗi phase phải tạo ra giá trị vận hành rõ ràng
- phân biệt rõ:
  - cần làm ngay trước release
  - nên làm sớm sau release
  - có thể mở rộng sau

## Cách đọc roadmap

### Priority

- `P0`: bắt buộc nên làm trước release
- `P1`: rất nên làm sớm, ảnh hưởng trực tiếp đến chất lượng vận hành
- `P2`: mở rộng quan trọng nhưng chưa phải blocker
- `P3`: nice-to-have

### Effort

- `S`: nhỏ
- `M`: trung bình
- `L`: lớn
- `XL`: rất lớn

## Phase 0: Stabilize Current Release

### Mục tiêu

Ổn định bản hiện tại để tránh lỗi nghiệp vụ rõ ràng khi chạy production sớm.

### Hạng mục

#### 0.1. Hậu-booking state guard

- Priority: `P0`
- Effort: `M`
- Mục tiêu:
  - không cho message kiểu `cảm ơn`, `ok`, `dạ` kích hoạt lại nhánh tạo booking
  - sau booking thành công phải đóng booking draft cũ

#### 0.2. Booking duplicate protection

- Priority: `P0`
- Effort: `M`
- Mục tiêu:
  - một confirmation không được tạo booking nhiều lần
  - thêm guard ở tầng business logic, không chỉ ở tầng channel

#### 0.3. Cheap-path cho low-value messages

- Priority: `P0`
- Effort: `S`
- Mục tiêu:
  - greeting
  - thanks
  - acknowledgement
  - yes/no ngắn
- Không nên chạy full AI flow cho mọi message ngắn

#### 0.4. Review lại luồng session hiện tại

- Priority: `P0`
- Effort: `S`
- Mục tiêu:
  - xác nhận chính xác vòng đời session hiện tại
  - ghi rõ session timeout
  - làm rõ session nào được dùng cho web, session nào dùng cho Messenger

#### 0.5. Chuẩn hóa log nghiệp vụ quan trọng

- Priority: `P1`
- Effort: `S`
- Mục tiêu:
  - log khi booking attempt bắt đầu
  - log khi booking thành công
  - log khi booking bị chặn vì duplicate
  - log khi hậu-booking acknowledgement được nhận

### Deliverable của Phase 0

- chatbot không bị lặp booking do hậu-booking acknowledgement
- giảm lỗi nghiệp vụ dễ thấy
- log dễ debug hơn khi có incident

## Phase 1: Booking Lifecycle Awareness

### Mục tiêu

Để bot hiểu user đang ở giai đoạn nào trong vòng đời booking.

### Hạng mục

#### 1.1. Active booking context

- Priority: `P0`
- Effort: `M`
- Mục tiêu:
  - khi booking thành công, tạo active booking context
  - context sống tới `check_out_date + grace period`

#### 1.2. Tách short-term session và booking context

- Priority: `P0`
- Effort: `M`
- Mục tiêu:
  - session chat gần chỉ giữ hội thoại ngắn hạn
  - booking context giữ nghiệp vụ booking active

#### 1.3. Hậu-booking intents

- Priority: `P1`
- Effort: `M`
- Tạo nhóm intent riêng:
  - `booking_acknowledgement`
  - `booking_lookup`
  - `booking_modification`
  - `booking_cancellation`
  - `pre_arrival_support`

#### 1.4. Response policy hậu-booking

- Priority: `P1`
- Effort: `S`
- Mục tiêu:
  - nếu user chỉ xã giao sau booking, trả lời ngắn
  - nếu user hỏi về booking, tra active booking trước

### Deliverable của Phase 1

- bot không chỉ “nhớ chat”, mà bắt đầu “hiểu vòng đời booking”

## Phase 2: Customer Identity & Long-Term Continuity

### Mục tiêu

Giúp bot hỗ trợ đúng khi user quay lại sau vài giờ, vài ngày hoặc nhiều tuần.

### Hạng mục

#### 2.1. Customer profile tối thiểu

- Priority: `P1`
- Effort: `M`
- Fields gợi ý:
  - `CustomerId`
  - `FullName`
  - `PrimaryPhone`
  - `Email`
  - `PreferredLanguage`
  - `LastBookingId`

#### 2.2. Channel identity mapping

- Priority: `P1`
- Effort: `M`
- Mục tiêu:
  - map `pageId + senderId` sang customer
  - cho phép một customer có nhiều channel identity

#### 2.3. Booking history lookup

- Priority: `P1`
- Effort: `M`
- Mục tiêu:
  - tra booking gần nhất của customer
  - hỗ trợ user quay lại sau 1-2 ngày

#### 2.4. Session resume policy

- Priority: `P1`
- Effort: `S`
- Mục tiêu:
  - xác định khi nào nối tiếp context cũ
  - khi nào bắt đầu session mới

### Deliverable của Phase 2

- user quay lại sau nhiều ngày vẫn được hỗ trợ đúng hơn
- giảm phụ thuộc vào memory cache ngắn hạn

## Phase 3: Temporal Understanding & Structured Extraction

### Mục tiêu

Giảm lỗi ngày giờ và tăng độ chắc chắn khi user nói kiểu tiếng Việt đời thường.

### Hạng mục

#### 3.1. Mở rộng parser thời gian tiếng Việt

- Priority: `P1`
- Effort: `M`
- Các pattern cần ưu tiên:
  - `mai`
  - `mốt`
  - `ngày kia`
  - `cuối tuần này`
  - `đầu tháng sau`
  - `2 đêm từ thứ 7 tuần tới`

#### 3.2. Chuẩn hóa quy tắc check-in/check-out extraction

- Priority: `P1`
- Effort: `M`
- Mục tiêu:
  - tách rõ ngày bắt đầu, ngày kết thúc, số đêm

#### 3.3. Structured extraction fallback

- Priority: `P2`
- Effort: `M`
- Mục tiêu:
  - nếu AI extraction mơ hồ, dùng deterministic parser để override

### Deliverable của Phase 3

- giảm sai ngày booking
- tăng độ chính xác cho flow đặt phòng

## Phase 4: Messenger & Channel Hardening

### Mục tiêu

Biến channel integration thành production-grade, không chỉ là webhook hoạt động.

### Hạng mục

#### 4.1. `pageId -> hotelId` mapping

- Priority: `P0`
- Effort: `M`
- Mục tiêu:
  - một hệ thống phục vụ nhiều Facebook Page
  - tránh nhầm dữ liệu khách sạn

#### 4.2. Message log chuẩn hóa

- Priority: `P1`
- Effort: `M`
- Lưu:
  - inbound
  - outbound
  - raw event
  - normalized message

#### 4.3. Attachment policy

- Priority: `P2`
- Effort: `M`
- Mục tiêu:
  - xác định cách xử lý image, voice, sticker, attachment only

#### 4.4. Signature verification

- Priority: `P2`
- Effort: `S`
- Mục tiêu:
  - verify webhook signature nếu cần tăng bảo mật

#### 4.5. Channel UX improvements

- Priority: `P3`
- Effort: `S`
- Ví dụ:
  - typing indicator
  - seen/read strategy

### Deliverable của Phase 4

- channel layer đủ chắc cho nhiều khách hàng thật

## Phase 5: RAG Completion & Knowledge Governance

### Mục tiêu

Tối ưu câu trả lời tri thức, giảm token và tăng độ chính xác cho FAQ/policy.

### Hạng mục

#### 5.1. Bật retrieval thật sự thay cho full-context nặng

- Priority: `P1`
- Effort: `M`
- Mục tiêu:
  - chỉ lấy chunk liên quan
  - không nhét toàn bộ hotel context mỗi turn

#### 5.2. Knowledge document curation

- Priority: `P1`
- Effort: `M`
- Mục tiêu:
  - chuẩn hóa FAQ
  - policy
  - room descriptions
  - promotions

#### 5.3. Reindex workflow

- Priority: `P1`
- Effort: `S`
- Mục tiêu:
  - update Google Sheets xong thì reindex được an toàn

#### 5.4. Retrieval quality controls

- Priority: `P2`
- Effort: `M`
- Mục tiêu:
  - top-k phù hợp
  - intent-aware filters
  - tránh retrieval noise

### Deliverable của Phase 5

- câu trả lời tri thức tốt hơn
- cost AI tối ưu hơn

## Phase 6: Booking Operations Expansion

### Mục tiêu

Bot không chỉ tạo booking mà còn xử lý được các nhu cầu thực tế sau booking.

### Hạng mục

#### 6.1. Booking lookup by booking id / phone

- Priority: `P1`
- Effort: `M`

#### 6.2. Cancellation flow

- Priority: `P1`
- Effort: `M`

#### 6.3. Modification flow

- Priority: `P2`
- Effort: `L`
- Ví dụ:
  - đổi ngày
  - đổi số khách
  - đổi loại phòng

#### 6.4. Pre-arrival support

- Priority: `P2`
- Effort: `S`
- Ví dụ:
  - giờ check-in
  - địa chỉ
  - hướng dẫn đến khách sạn

### Deliverable của Phase 6

- bot hữu dụng hơn nhiều sau khi booking xong

## Phase 7: Human Handoff & Service Desk

### Mục tiêu

Đảm bảo bot không tự xử lý quá khả năng.

### Hạng mục

#### 7.1. Escalation rules

- Priority: `P1`
- Effort: `S`
- Case:
  - khiếu nại
  - tức giận
  - case không chắc
  - đoàn đông

#### 7.2. Handoff queue

- Priority: `P2`
- Effort: `M`

#### 7.3. Agent notes / summary

- Priority: `P2`
- Effort: `M`
- Mục tiêu:
  - bot tóm tắt để nhân viên tiếp nhận nhanh hơn

### Deliverable của Phase 7

- bot biết lúc nào nên dừng và chuyển người thật

## Phase 8: Analytics, Cost, and Productization

### Mục tiêu

Biến chatbot thành sản phẩm có thể theo dõi, tối ưu và định giá tốt.

### Hạng mục

#### 8.1. Usage dashboard

- Priority: `P1`
- Effort: `M`
- Theo dõi:
  - message count
  - token
  - cost
  - by hotel
  - by channel

#### 8.2. Funnel analytics

- Priority: `P1`
- Effort: `M`
- Theo dõi:
  - inquiry -> availability -> booking confirm -> booking success

#### 8.3. Intent quality review

- Priority: `P2`
- Effort: `M`

#### 8.4. Pricing recommendation engine

- Priority: `P2`
- Effort: `M`
- Dựa trên usage thật của từng hotel

### Deliverable của Phase 8

- có đủ số liệu để vận hành và pricing product

## Gợi ý thứ tự triển khai thực tế

### Nếu sắp release

Nên làm trước:

1. Phase 0
2. Phase 1
3. phần quan trọng của Phase 4
4. phần quan trọng của Phase 2

### Nếu đã release và bắt đầu có khách dùng thật

Nên ưu tiên tiếp:

1. Phase 6
2. Phase 5
3. Phase 8
4. Phase 7

## Backlog ưu tiên rút gọn

### P0

- hậu-booking state guard
- duplicate booking protection
- cheap-path cho low-value messages
- active booking context
- `pageId -> hotelId` mapping

### P1

- customer identity mapping
- booking history lookup
- hậu-booking intents
- temporal parsing mở rộng
- message logging chuẩn
- usage dashboard

### P2

- booking modification flow
- attachment handling
- retrieval quality controls
- handoff queue
- pricing recommendation

### P3

- typing indicator
- richer personalization
- advanced analytics

## Suggested Team Split

### Backend core

- booking lifecycle
- session/context
- customer memory
- business services

### Channel integration

- Messenger
- web chat
- future Zalo/WhatsApp

### AI/RAG

- intent
- extraction
- retrieval
- prompt/context hygiene

### Product/ops

- analytics
- handoff
- pricing
- onboarding

## Definition of Done cho bản production tốt

Một bản build được xem là đủ tốt khi:

- không double booking
- không bị hậu-booking bug ở các tin nhắn xã giao
- user quay lại sau vài ngày vẫn được hỗ trợ đúng booking gần nhất
- message ngắn không làm flow AI chạy sai
- nhiều message liên tiếp không gây reply giữa chừng
- routing đúng hotel
- có đủ log để debug incident
- đo được cost AI và conversion booking

## Kết luận

Roadmap tốt nhất cho sản phẩm này không phải là "thêm thật nhiều AI", mà là:

- khóa chặt state và nghiệp vụ trước
- làm continuity đúng
- rồi mới tối ưu intelligence và scale

Nếu đi đúng thứ tự, bạn sẽ có một chatbot:

- an toàn hơn
- thông minh hơn
- rẻ hơn để vận hành
- và dễ bán hơn cho khách sạn thật.
