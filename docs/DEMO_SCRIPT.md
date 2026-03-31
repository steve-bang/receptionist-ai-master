# Demo Script

## Mục tiêu

Tài liệu này giúp trình bày demo Hotel AI Chatbot Platform một cách ngắn gọn, rõ ràng và dễ thuyết phục với:

- chủ khách sạn
- quản lý vận hành
- lễ tân
- đối tác triển khai

Mục tiêu của buổi demo không phải là khoe kỹ thuật, mà là giúp khách hàng thấy ngay:

- bot trả lời nhanh
- bot hiểu nhu cầu khách
- bot hỗ trợ booking thực tế
- bot giúp giảm tải cho lễ tân
- bot có thể triển khai được trên kênh họ đang dùng

## Thời lượng đề xuất

- `15-20 phút` cho demo ngắn
- `30-45 phút` cho demo có hỏi đáp và trao đổi nhu cầu

## Chuẩn bị trước buổi demo

### 1. Chuẩn bị dữ liệu khách sạn

Nên dùng:

- tên khách sạn thật
- dữ liệu phòng thật hoặc dữ liệu seed gần đúng
- FAQ thật
- chính sách thật
- khuyến mãi thật nếu có

### 2. Chuẩn bị kênh demo

Ít nhất nên có:

- Web chat
- hoặc Facebook Messenger

### 3. Chuẩn bị use case mẫu

Nên có sẵn 5 nhóm câu hỏi:

- hỏi thông tin cơ bản
- hỏi giá/phòng trống
- tư vấn loại phòng
- tạo booking
- hỏi lại sau booking

### 4. Chuẩn bị thông điệp bán hàng

Bạn cần nói rõ:

- bot này giúp gì cho khách sạn
- bot không thay người hoàn toàn
- bot giúp giảm tải và tăng phản hồi

## Cấu trúc buổi demo

## Phần 1: Mở đầu

### Mục tiêu

Thiết lập kỳ vọng đúng ngay từ đầu.

### Cách nói gợi ý

"Bên em đang xây một lễ tân AI chuyên cho khách sạn và homestay. Điểm khác là bot không chỉ trả lời FAQ, mà còn hỗ trợ tư vấn phòng, hỗ trợ booking và bám dữ liệu thật của khách sạn."

"Hôm nay em sẽ demo theo đúng hành trình mà khách thường nhắn tin, để anh/chị nhìn thấy rõ bot hỗ trợ được những gì trong thực tế."

## Phần 2: Demo use case 1 - FAQ cơ bản

### Mục tiêu

Chứng minh bot trả lời nhanh, tự nhiên và đúng thông tin cơ bản.

### Câu hỏi mẫu

- `Khách sạn mình ở đâu vậy?`
- `Check-in lúc mấy giờ?`
- `Có hồ bơi không?`
- `Có ăn sáng không?`

### Điều cần nhấn mạnh

- bot trả lời nhanh
- thông tin thống nhất
- giảm tải cho lễ tân ở các câu hỏi lặp lại

### Cách nói gợi ý

"Đây là nhóm câu hỏi mà lễ tân thường phải trả lời rất nhiều lần mỗi ngày. Bot giúp xử lý tự động phần này để đội ngũ tập trung vào việc quan trọng hơn."

## Phần 3: Demo use case 2 - Hỏi phòng và giá

### Mục tiêu

Chứng minh bot hỗ trợ khách trong hành trình cân nhắc booking.

### Câu hỏi mẫu

- `Cuối tuần này còn phòng không?`
- `Phòng cho 2 người giá bao nhiêu?`
- `Có phòng view biển không?`

### Điều cần nhấn mạnh

- bot không chỉ nói chung chung
- bot có thể dựa trên dữ liệu phòng và availability
- giúp khách đi nhanh hơn đến bước ra quyết định

### Cách nói gợi ý

"Ở đây bot không chỉ trả lời kiểu FAQ, mà bắt đầu hỗ trợ theo nhu cầu cụ thể của khách: ngày ở, số người, loại phòng."

## Phần 4: Demo use case 3 - Tư vấn loại phòng

### Mục tiêu

Chứng minh bot có thể đóng vai trò tư vấn bán hàng.

### Câu hỏi mẫu

- `Mình đi 2 người, muốn phòng đẹp một chút thì nên chọn loại nào?`
- `Có phòng nào phù hợp cho gia đình không?`
- `Phòng nào được khách chọn nhiều?`

### Điều cần nhấn mạnh

- bot không chỉ trả lời dữ liệu khô
- bot hỗ trợ định hướng lựa chọn
- bot giúp tăng khả năng chốt booking

### Cách nói gợi ý

"Đây là phần rất quan trọng vì bot không chỉ trả lời đúng, mà còn giúp tư vấn giống như một lễ tân đang hỗ trợ bán phòng."

## Phần 5: Demo use case 4 - Booking flow

### Mục tiêu

Chứng minh bot có thể đi từ hội thoại đến booking thật.

### Flow demo đề xuất

1. User hỏi phòng
2. Bot tư vấn
3. User đồng ý đặt
4. Bot hỏi thêm thông tin còn thiếu
5. Bot xác nhận lại
6. Bot tạo booking thành công

### Câu nhắn mẫu

- `Anh muốn đặt phòng cho 2 người vào thứ 7 tuần tới`
- `Ở 1 đêm`
- `Loại view biển`
- `Tên anh là Sang`
- `Số điện thoại là 09xxxxxxxx`
- `Ok xác nhận giúp anh`

### Điều cần nhấn mạnh

- bot thu thập thông tin từng bước
- không tự bịa booking thành công
- chỉ trả về booking khi backend tạo thành công

### Cách nói gợi ý

"Phần này là điểm khác biệt quan trọng. Bot không chỉ nói như thật, mà phải bám logic backend và dữ liệu thật để tạo booking đúng."

## Phần 6: Demo use case 5 - Sau booking

### Mục tiêu

Chứng minh bot không chỉ dừng ở bước chốt booking.

### Câu hỏi mẫu

- `Cho tôi xin lại mã booking`
- `Check-in lúc mấy giờ nhỉ?`
- `Tôi muốn hỏi lại thông tin booking`

### Điều cần nhấn mạnh

- bot có thể hỗ trợ tiếp sau khi đã đặt
- đây là nhu cầu thực tế rất thường gặp

### Lưu ý

Nếu tính năng hậu-booking chưa hoàn chỉnh 100%, hãy demo mức an toàn và nói rõ:

"Phần hỗ trợ hậu-booking bên em đang tiếp tục hoàn thiện để bot nhớ được booking theo vòng đời tốt hơn."

## Phần 7: Demo đa kênh

### Mục tiêu

Giúp khách hàng thấy bot có thể dùng trên kênh họ quan tâm.

### Gợi ý trình bày

- web chat
- Facebook Messenger

### Cách nói gợi ý

"Thay vì bắt đội ngũ trả lời thủ công trên nhiều kênh, bot có thể hỗ trợ khách ngay trên nơi họ đang nhắn tin."

## Phần 8: Giải thích giá trị kinh doanh

### 3 thông điệp chính

1. Giảm tải cho lễ tân
2. Không bỏ lỡ khách nhắn ngoài giờ
3. Tăng cơ hội chốt booking

### Cách nói gợi ý

"Giá trị lớn nhất không chỉ là trả lời nhanh, mà là không bỏ lỡ khách hàng đang có nhu cầu thật, đặc biệt ngoài giờ hoặc lúc lễ tân bận."

## Câu hỏi gợi mở cho khách hàng

Sau phần demo, nên hỏi:

- `Hiện tại khách hay hỏi bên mình những gì nhiều nhất?`
- `Page hoặc web của mình hiện có nhiều khách nhắn ngoài giờ không?`
- `Đội lễ tân đang tốn thời gian nhất ở phần nào?`
- `Nếu bot xử lý tốt 60-70% câu hỏi lặp thì có hữu ích cho bên mình không?`

Mục tiêu là kéo khách hàng nói về pain point thật của họ.

## Objection handling trong lúc demo

### 1. "AI có nói sai không?"

Gợi ý trả lời:

"Có thể có rủi ro nếu để AI tự nói mọi thứ. Vì vậy bên em thiết kế theo hướng các phần nghiệp vụ như availability, booking, trạng thái booking phải dựa trên dữ liệu thật."

### 2. "Triển khai có khó không?"

Gợi ý trả lời:

"Giai đoạn đầu bên em setup chính cho mình. Bên mình chỉ cần cung cấp dữ liệu cần thiết và cấp quyền các kênh nếu muốn kết nối."

### 3. "Bot có thay hết lễ tân không?"

Gợi ý trả lời:

"Mục tiêu không phải thay hoàn toàn lễ tân, mà là giúp xử lý phần câu hỏi lặp và lọc trước nhu cầu của khách, để đội ngũ tập trung vào các trường hợp quan trọng hơn."

### 4. "Nếu khách hỏi khó thì sao?"

Gợi ý trả lời:

"Những trường hợp phức tạp hoặc nhạy cảm thì nên chuyển cho người thật. Bot tốt là bot biết giới hạn của mình."

## Cách chốt buổi demo

### Mục tiêu

Không để buổi demo kết thúc mơ hồ.

### Cách chốt gợi ý

"Nếu anh/chị thấy hướng này phù hợp, bên em có thể đề xuất một gói khởi đầu để chạy thử trên một kênh trước, ví dụ web chat hoặc Messenger, rồi theo dõi hiệu quả thực tế trong tháng đầu."

### Call to action phù hợp

- chốt buổi technical setup
- chốt pilot
- chốt trial
- chốt buổi review dữ liệu khách sạn

## Demo checklist nhanh

- dữ liệu khách sạn đã sẵn sàng
- bot đang trả lời đúng tiếng Việt
- kênh demo hoạt động
- có sẵn use case booking
- có sẵn use case hậu-booking
- chuẩn bị trước 2-3 objection thường gặp
- biết rõ bước tiếp theo muốn chốt với khách

## Những điều nên tránh khi demo

- nói quá nhiều về model AI
- sa vào chi tiết kỹ thuật quá sớm
- demo với dữ liệu giả quá lộ
- để demo kéo dài nhưng không đi đến booking flow
- hứa những tính năng chưa ổn định

## Kết luận

Một buổi demo tốt phải giúp khách hàng thấy rõ ba điều:

- chatbot này thực sự dùng được cho khách sạn
- chatbot này giúp giảm tải và tăng cơ hội chốt booking
- chatbot này triển khai được với chi phí và độ phức tạp hợp lý

Nếu khách hàng nhìn thấy ba điều đó, khả năng chốt pilot hoặc trial sẽ cao hơn rất nhiều.
