# Product Requirements Document

## Tên sản phẩm

Hotel AI Chatbot Platform

## Tổng quan sản phẩm

Hotel AI Chatbot Platform là một nền tảng chatbot AI chuyên biệt cho ngành lưu trú, giúp khách sạn, nhà nghỉ, homestay và boutique hotel tự động hóa kênh chat để:

- trả lời khách nhanh hơn
- tư vấn phòng chính xác hơn
- hỗ trợ tạo booking
- giảm tải cho lễ tân
- tăng tỷ lệ chuyển đổi từ hội thoại sang booking

Sản phẩm không chỉ là một chatbot FAQ, mà hướng tới vai trò "lễ tân AI" có thể hỗ trợ trước booking, trong booking và sau booking.

## Bối cảnh

Phần lớn cơ sở lưu trú nhỏ và vừa tại Việt Nam gặp các vấn đề sau:

- khách nhắn tin ngoài giờ nhưng không được phản hồi nhanh
- lễ tân phải trả lời lặp đi lặp lại cùng một nhóm câu hỏi
- thông tin giữa các nhân viên không đồng nhất
- việc ghi nhận booking qua chat còn thủ công
- khó kiểm soát chất lượng chăm sóc khách hàng qua nhiều kênh

Trong khi đó, các giải pháp enterprise hiện có thường:

- quá đắt
- quá phức tạp
- không tối ưu cho cách khách Việt nhắn tin thực tế

## Mục tiêu sản phẩm

### Mục tiêu kinh doanh

- xây dựng một sản phẩm AI chatbot có thể bán thực tế cho phân khúc lưu trú tại Việt Nam
- tạo doanh thu thuê bao hàng tháng theo mô hình SaaS
- giữ cost AI đủ thấp để pricing phù hợp thị trường
- hỗ trợ mở rộng từ 1 khách sạn sang nhiều khách sạn

### Mục tiêu người dùng

- khách được phản hồi nhanh 24/7
- khách được tư vấn phòng đúng nhu cầu
- khách được hỗ trợ đặt phòng thuận tiện
- khách có thể quay lại hỏi tiếp về booking đã tạo

### Mục tiêu vận hành

- giảm tải khối lượng chat thủ công cho lễ tân
- tăng khả năng chốt booking từ các kênh chat
- giúp chủ khách sạn theo dõi được usage, cost và hiệu quả chatbot

## Phạm vi sản phẩm

## Trong phạm vi

- trả lời FAQ về khách sạn
- tư vấn loại phòng
- kiểm tra availability
- cung cấp thông tin giá
- hỗ trợ thu thập thông tin để tạo booking
- tạo booking thật qua backend
- hỗ trợ một phần hậu-booking
- tích hợp web chat
- tích hợp Facebook Messenger
- hỗ trợ RAG cho tri thức khách sạn
- theo dõi usage và cost AI

## Ngoài phạm vi giai đoạn đầu

- thay thế hoàn toàn PMS
- xử lý mọi nghiệp vụ vận hành nội bộ khách sạn
- voice bot hoàn chỉnh
- CRM/hệ loyalty sâu
- thanh toán online full flow
- OTA management

## Đối tượng người dùng

### 1. Khách hàng cuối

Người nhắn tin với chatbot để:

- hỏi phòng
- hỏi giá
- hỏi chính sách
- đặt phòng
- hỏi lại thông tin booking

### 2. Chủ khách sạn / quản lý

Người cần:

- triển khai chatbot nhanh
- không cần setup kỹ thuật phức tạp
- theo dõi chi phí và hiệu quả

### 3. Nhân viên lễ tân / CSKH

Người cần:

- giảm tải câu hỏi lặp
- tiếp nhận các case phức tạp khi bot handoff

## Vấn đề cốt lõi cần giải quyết

1. Khách hàng nhắn tin nhưng phản hồi chậm, dễ mất booking.
2. Lễ tân bị phân tán thời gian bởi nhiều câu hỏi lặp.
3. Chatbot thông thường thường nói hay nhưng không bám dữ liệu thật.
4. Booking qua chat dễ sai ngày, sai thông tin hoặc bị trùng.
5. Khó mở rộng một chatbot dùng cho nhiều khách sạn nếu không có cấu trúc đúng.

## Giá trị cốt lõi của sản phẩm

### 1. AI nhưng phải bám dữ liệu thật

Các nghiệp vụ như:

- availability
- pricing
- booking
- trạng thái booking

phải dựa trên source of truth chứ không để AI tự quyết.

### 2. Tối ưu cho hành vi chat thực tế ở Việt Nam

Sản phẩm phải xử lý tốt:

- tin nhắn ngắn
- nhiều tin liên tiếp
- ngôn ngữ mơ hồ
- ngày tháng kiểu đời thường

### 3. Dễ bán, dễ triển khai

Khách sạn không nên phải tự cấu hình hệ thống phức tạp.

### 4. Scale được

Phải hỗ trợ:

- nhiều khách sạn
- nhiều kênh
- nhiều phiên hội thoại
- nhiều use case nghiệp vụ

## Functional Requirements

## FR1. Quản lý tri thức khách sạn

Hệ thống phải cho phép chatbot truy cập dữ liệu:

- thông tin khách sạn
- phòng
- tiện ích
- FAQ
- promotions
- policies

Nguồn dữ liệu giai đoạn hiện tại:

- Google Sheets

## FR2. Chat and intent handling

Hệ thống phải:

- nhận message từ user
- phân tích intent
- trích xuất entity quan trọng
- tạo câu trả lời phù hợp
- giữ session ngắn hạn

## FR3. Availability and pricing

Hệ thống phải:

- tra cứu availability theo ngày
- tính giá theo room type và số đêm
- áp promotion khi phù hợp

## FR4. Booking creation

Hệ thống phải:

- thu thập đủ dữ liệu booking
- xác nhận thông tin
- tạo booking thật trên backend
- trả booking confirmation chuẩn

## FR5. Post-booking support

Hệ thống nên hỗ trợ:

- trả lại mã booking
- cung cấp thông tin check-in/check-out
- hỗ trợ các câu hỏi cơ bản sau booking

## FR6. Messenger integration

Hệ thống phải:

- nhận webhook Messenger
- chống duplicate cơ bản
- debounce message liên tiếp
- gửi reply lại đúng user

## FR7. Usage and cost tracking

Hệ thống phải:

- track prompt tokens
- track completion tokens
- track total tokens
- lưu estimated AI cost
- tổng hợp usage theo session, hotel, model, operation

## FR8. RAG integration

Hệ thống phải có khả năng:

- index knowledge vào vector database
- retrieve đúng chunk liên quan
- giảm phụ thuộc vào full prompt context

## FR9. Multi-hotel support

Hệ thống phải có khả năng mở rộng để:

- nhiều khách sạn dùng chung nền tảng
- mỗi khách sạn có data riêng
- mỗi channel/account map đúng về hotel tương ứng

## Non-Functional Requirements

## NFR1. Độ chính xác nghiệp vụ

- không được tự tuyên bố booking thành công nếu backend chưa tạo booking thật
- không được trả lời availability hoặc booking status trái với dữ liệu thật

## NFR2. Tính ổn định

- hệ thống phải chịu được duplicate webhook
- nhiều message liên tiếp không được trả lời giữa chừng
- lỗi channel không được làm hỏng workflow lõi

## NFR3. Hiệu năng

- webhook phải ack nhanh
- chatbot phản hồi đủ nhanh cho use case chat thời gian thực

## NFR4. Khả năng mở rộng

- thêm khách sạn mới không làm thay đổi kiến trúc lõi
- thêm channel mới không làm vỡ business workflow

## NFR5. Kiểm soát chi phí

- hạn chế gọi AI không cần thiết
- hỗ trợ cheap-path cho message low-value
- tối ưu context và RAG

## User Stories

### Khách hỏi thông tin

- Là một khách hàng, tôi muốn hỏi giá phòng và tiện ích để quyết định có nên đặt phòng không.

### Khách muốn đặt phòng

- Là một khách hàng, tôi muốn chatbot hướng dẫn tôi từng bước để hoàn tất booking nhanh chóng.

### Khách quay lại sau booking

- Là một khách hàng, tôi muốn hỏi lại thông tin booking của mình mà không phải nhập lại mọi thứ từ đầu.

### Chủ khách sạn

- Là một chủ khách sạn, tôi muốn chatbot hoạt động ổn định và chi phí hợp lý để có thể duy trì hàng tháng.

### Lễ tân

- Là một nhân viên lễ tân, tôi muốn chatbot xử lý phần lớn câu hỏi lặp lại để tôi tập trung vào các case cần người thật.

## Success Metrics

### Product metrics

- số message xử lý mỗi ngày
- tỷ lệ chatbot phản hồi thành công
- tỷ lệ hội thoại đi tới booking flow
- tỷ lệ booking thành công

### Business metrics

- số khách sạn active
- MRR
- cost AI trên mỗi khách sạn
- cost AI trên mỗi message / conversation

### Experience metrics

- tỷ lệ handoff hợp lý
- số incident duplicate booking
- số lỗi sai ngày / sai ngữ cảnh

## MVP Definition

Một phiên bản MVP tốt nên đạt:

- trả lời FAQ tốt
- hỗ trợ hỏi phòng, giá
- tạo booking cơ bản
- có usage tracking
- có Messenger webhook hoạt động
- có cơ chế chống lỗi duplicate cơ bản

## Release Risks

### Rủi ro cao

- duplicate booking sau khi confirm
- session không phản ánh đúng lifecycle booking
- user quay lại sau vài ngày nhưng bot mất ngữ cảnh hoàn toàn
- low-value messages vẫn kích hoạt flow không đúng

### Rủi ro trung bình

- date parsing tiếng Việt chưa đủ rộng
- Messenger multi-page routing chưa đầy đủ
- hậu-booking support chưa mạnh

## Product Principles

1. Nói đúng quan trọng hơn nói hay.
2. Dữ liệu thật quan trọng hơn suy luận tự do.
3. Trải nghiệm triển khai phải đơn giản cho khách sạn.
4. Phải tối ưu cost để phù hợp thị trường Việt Nam.
5. Mỗi bước mở rộng phải giữ được an toàn nghiệp vụ.

## Roadmap Summary

### Giai đoạn 1

- FAQ
- booking cơ bản
- web + Messenger
- usage tracking

### Giai đoạn 2

- booking lifecycle awareness
- customer memory
- hậu-booking support
- multi-hotel hardening

### Giai đoạn 3

- handoff
- analytics dashboard
- richer RAG
- nhiều channel hơn

## Kết luận

PRD này xác định sản phẩm là một hotel chatbot AI thiên về vận hành thực tế, không chỉ là lớp giao diện AI.

Trọng tâm của sản phẩm là:

- dùng AI để nâng trải nghiệm hội thoại
- dùng business logic và dữ liệu thật để bảo đảm tính đúng
- tạo ra một giải pháp dễ bán và đủ bền để vận hành cho ngành lưu trú tại Việt Nam.
