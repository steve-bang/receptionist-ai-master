# Founder Journal

## Hành trình xây dựng Hotel AI Chatbot

Có những dự án bắt đầu từ một ý tưởng công nghệ.

Nhưng với tôi, dự án này không bắt đầu từ công nghệ. Nó bắt đầu từ một câu hỏi rất thực tế:

**"Liệu có thể tạo ra một chatbot AI thực sự hữu ích cho khách sạn, homestay và nhà nghỉ tại Việt Nam hay không?"**

Không phải một chatbot để demo.
Không phải một chatbot chỉ để trả lời cho hay.
Mà là một chatbot có thể vận hành thật, hỗ trợ khách thật, giúp chốt booking thật và giải được bài toán thật của ngành lưu trú.

## Vì sao tôi bắt đầu dự án này

Tôi nhận ra một điều khá rõ:

Rất nhiều khách sạn nhỏ, homestay, boutique hotel hay resort mini ở Việt Nam không thiếu khách hỏi. Thứ họ thiếu là khả năng phản hồi nhanh, đều và chuyên nghiệp trên các kênh chat.

Khách thường nhắn:

- còn phòng không
- giá bao nhiêu
- check-in lúc mấy giờ
- có ăn sáng không
- có hồ bơi không
- đặt phòng như thế nào

Những câu hỏi đó lặp đi lặp lại mỗi ngày.

Nếu có người trực tốt, cơ hội chốt booking cao.
Nếu trả lời chậm, khách đi nơi khác.

Tôi thấy ở đây có một khoảng trống rất lớn:

- doanh nghiệp nhỏ thì cần tự động hóa
- nhưng các giải pháp hiện có thường quá phức tạp, quá đắt hoặc quá chung chung
- còn chatbot "AI" ngoài kia thì nhiều cái trả lời nghe có vẻ hay, nhưng lại không giúp được gì nhiều cho vận hành thật

Từ đó, tôi muốn xây một sản phẩm thực dụng hơn.

## Tôi không muốn xây một chatbot chỉ để nói hay

Ngay từ đầu, điều tôi tự nhắc mình là:

**Sản phẩm này không được dừng ở việc trả lời mượt.**

Nó phải:

- hiểu được khách đang cần gì
- bám dữ liệu thật của khách sạn
- hỗ trợ đặt phòng đúng logic
- không gây lỗi nghiệp vụ
- và quan trọng là phải phù hợp với cách người Việt thật sự nhắn tin

Đây là khác biệt mà tôi ngày càng thấy rõ trong quá trình làm:

Một chatbot để demo thì rất dễ.
Một chatbot để vận hành thật thì khó hơn rất nhiều.

## Càng làm, tôi càng hiểu bài toán lớn nhất không phải là AI

Lúc đầu, như nhiều người khác, tôi nghĩ thử thách lớn nhất sẽ là:

- chọn model nào
- viết prompt ra sao
- tích hợp AI thế nào

Nhưng càng đi sâu, tôi càng thấy:

**Vấn đề lớn nhất không nằm ở AI.**

Vấn đề lớn nhất nằm ở:

- logic nghiệp vụ
- trạng thái hội thoại
- vòng đời booking
- sự chính xác của dữ liệu
- và khả năng kiểm soát những gì bot được phép làm

Tôi bắt đầu thấy rất rõ một nguyên tắc:

AI có thể giúp bot nói chuyện tự nhiên hơn.
Nhưng backend mới là nơi phải bảo vệ sự đúng đắn của hệ thống.

Đó là một bài học rất quan trọng với tôi.

## Những lúc sản phẩm khiến tôi phải dừng lại và suy nghĩ lại

Có những bug nhìn bề ngoài rất nhỏ, nhưng lại nói lên bản chất rất lớn của hệ thống.

Ví dụ:

Khách vừa booking xong.
Sau đó chỉ nhắn một câu rất đơn giản như:

`cảm ơn`

Nhưng hệ thống lại cố tạo booking lần nữa, rồi trả về lỗi.

Khi nhìn thấy tình huống đó, tôi hiểu rằng:

Bot không chỉ cần biết "trả lời gì".
Bot còn phải biết:

- khách đang ở giai đoạn nào
- booking đã hoàn tất chưa
- lúc nào nên tiếp tục hỗ trợ
- lúc nào tuyệt đối không được kích hoạt lại một flow nghiệp vụ

Đây là lúc tôi nhận ra rằng nếu muốn đi đến production thật, hệ thống phải có:

- trí nhớ ngắn hạn
- trí nhớ theo booking
- trí nhớ dài hạn về khách hàng

Nói cách khác, bot phải có ngữ cảnh.

Không có ngữ cảnh, AI chỉ là một người trả lời rất nhanh nhưng không đủ đáng tin.

## Điều tôi học được về khách hàng thực tế

Khách hàng thật không chat như test case.

Họ nhắn:

- ngắn
- rời rạc
- nhiều message liên tiếp
- không rõ ý
- đổi ý liên tục
- dùng ngày tháng rất đời thường

Ví dụ:

- thứ 7 tuần tới
- ngày 13 tháng sau
- mai
- ở 1 đêm thôi
- như cũ nhé

Chính những điều đó khiến tôi phải nhìn sản phẩm này không phải như một chatbot AI thông thường, mà như một hệ thống cần hiểu hành vi giao tiếp rất đời thực.

Tôi cũng học được rằng:

Nếu bắt khách hàng tự làm quá nhiều bước kỹ thuật, họ sẽ nản rất nhanh.

Điều đó đúng cả với khách sử dụng chatbot lẫn khách hàng mua sản phẩm.

Vì vậy, tôi ngày càng tin vào hướng đi:

- sản phẩm phải mạnh về kỹ thuật
- nhưng trải nghiệm triển khai phải thật đơn giản

## Tôi bắt đầu nhìn sản phẩm như một "lễ tân AI"

Sau một thời gian, tôi không còn thấy đây chỉ là một chatbot nữa.

Tôi bắt đầu nhìn nó như một vai trò vận hành:

**một lễ tân AI**

Một lễ tân AI tốt phải làm được những việc rất đời thường:

- chào khách
- trả lời câu hỏi cơ bản
- tư vấn phòng
- hỗ trợ đặt phòng
- hỗ trợ sau booking
- biết lúc nào cần chuyển cho người thật

Và một lễ tân AI tốt không được:

- nói sai sự thật
- xác nhận booking khi chưa có dữ liệu thật
- khiến khách rối
- hay tạo thêm việc cho đội vận hành

Đây là góc nhìn giúp tôi định hình lại rất nhiều quyết định của sản phẩm.

## Điều tôi muốn giữ cho sản phẩm này

Tôi muốn sản phẩm này có những đặc điểm rất rõ:

### 1. Thực dụng

Tôi không muốn làm một sản phẩm chỉ đẹp trong slide.
Tôi muốn nó giải được việc hàng ngày.

### 2. Đủ thông minh nhưng không phức tạp quá mức

Tôi không tin rằng sản phẩm tốt phải phức tạp.
Tôi tin rằng sản phẩm tốt là sản phẩm biết xử lý đúng những thứ quan trọng nhất.

### 3. Phù hợp với thị trường Việt Nam

Tôi muốn sản phẩm này có mức giá mà khách sạn nhỏ và vừa có thể tiếp cận.
Tôi muốn cách triển khai đủ đơn giản để họ không cảm thấy bị "ngợp".

### 4. Tăng giá trị thật cho khách hàng

Nếu chatbot chỉ khiến khách sạn thấy "hay", vậy là chưa đủ.
Nó phải giúp họ:

- phản hồi nhanh hơn
- giảm tải hơn
- chốt booking tốt hơn

## Điều tôi thấy rõ về chặng đường phía trước

Tôi nghĩ dự án này mới chỉ ở đầu hành trình.

Phần thú vị là nền tảng đã bắt đầu hình thành.
Nhưng phần khó hơn vẫn còn ở phía trước:

- làm cho bot hiểu booking lifecycle tốt hơn
- giúp bot nhớ khách quay lại sau vài ngày
- xử lý tốt hơn các tình huống hậu-booking
- kết nối nhiều kênh hơn
- làm sản phẩm đủ ổn để scale nhiều khách sạn

Tôi cũng thấy rất rõ rằng:

Muốn đi đường dài, sản phẩm này không thể chỉ dựa vào prompt.
Nó cần:

- state rõ ràng
- data chuẩn
- business logic chắc
- và một trải nghiệm triển khai đủ tốt để khách hàng tin tưởng

## Về mặt cá nhân, dự án này dạy tôi điều gì

Dự án này làm tôi hiểu rõ hơn sự khác nhau giữa:

- một ý tưởng hay
- một sản phẩm có thể bán
- và một hệ thống có thể vận hành

Nó cũng nhắc tôi rằng:

Trong AI, rất dễ bị cuốn vào việc chạy theo model, prompt và những thứ hào nhoáng.
Nhưng thứ tạo ra giá trị bền vững vẫn là:

- hiểu đúng bài toán
- đi đủ sâu vào ngữ cảnh người dùng
- và kiên nhẫn xây từng lớp nền tảng một

Tôi thấy đây không chỉ là một dự án công nghệ.
Nó là một quá trình học cách biến AI thành một công cụ thực sự hữu ích cho một ngành nghề cụ thể.

## Tôi muốn dự án này đi đến đâu

Tôi muốn trong tương lai, khi một khách sạn nhỏ ở Việt Nam nghĩ đến chuyện tự động hóa kênh chat, họ không cảm thấy đó là thứ quá xa vời.

Tôi muốn họ có thể dùng một giải pháp:

- dễ triển khai
- dễ hiểu
- chi phí hợp lý
- đủ thông minh
- và đủ đáng tin để giao cho nó những tương tác đầu tiên với khách hàng

Nếu làm được điều đó, tôi nghĩ sản phẩm này sẽ có ý nghĩa thật sự.

Không phải vì nó dùng AI.
Mà vì nó giúp một doanh nghiệp nhỏ vận hành tốt hơn mỗi ngày.

## Kết

Nhìn lại chặng đường hiện tại, tôi không nghĩ đây là câu chuyện của một chatbot.

Tôi nghĩ đây là câu chuyện về việc xây dựng một hệ thống đủ hiểu con người, đủ hiểu nghiệp vụ và đủ thực tế để tạo ra giá trị ngoài đời.

Hành trình vẫn còn dài.
Sản phẩm vẫn còn nhiều thứ phải hoàn thiện.

Nhưng tôi tin hướng đi này là đúng.

Và nếu giữ được tinh thần:

- thực dụng
- chính xác
- tử tế với người dùng
- và kỷ luật với sản phẩm

thì dự án này hoàn toàn có thể trở thành một nền tảng AI hữu ích thật sự cho ngành lưu trú tại Việt Nam.
