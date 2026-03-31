# Facebook Messenger Webhook Setup

Updated: 2026-03-29
Project: ChatHotelAIBackend

## Mục tiêu

Tài liệu này được viết theo hướng rollout thực tế hiện tại:

- **Hướng 1: bên mình setup thủ công cho khách hàng**
- khách sạn không phải đụng vào Meta Developer
- khách chỉ cần cấp quyền Page cho bên mình

Tài liệu này giúp bạn hoàn thành đúng thứ tự 3 việc:

1. Dùng **Meta App trung tâm của hệ thống** để kết nối một Facebook Page của khách sạn
2. Verify được webhook callback vào WebAPI
3. Nhận được webhook POST từ Facebook Messenger vào API của bạn

Phạm vi hiện tại:

- cấu hình được
- kết nối được đến WebAPI
- nhận được event webhook thật

Chưa bao gồm bước gửi reply ngược lại cho Messenger Send API. Đó là bước tiếp theo.

## Chiến lược triển khai hiện tại

Hiện tại nên dùng:

- **1 Meta App trung tâm của bạn**
- **1 webhook endpoint dùng chung**
- nhiều Facebook Page của nhiều khách sạn cùng kết nối vào hệ thống

Khách hàng không cần:

- tự tạo Meta App
- tự cấu hình webhook
- tự generate token
- tự vào Meta Developer Dashboard

Khách hàng chỉ cần làm một trong các cách sau:

1. thêm bạn làm admin/editor của Facebook Page
2. hoặc cấp quyền phù hợp để bạn tự kết nối Page đó

Sau đó toàn bộ phần kỹ thuật sẽ do bạn thao tác.

## Mô hình vận hành

Mô hình đúng ở giai đoạn này là:

- Facebook Page của khách sạn
- kết nối vào Meta App trung tâm của bạn
- Meta gửi webhook về WebAPI của bạn
- backend của bạn xử lý chatbot

Nói ngắn gọn:

- **Page là kênh chat**
- **Meta App là cầu nối**
- **WebAPI của bạn là backend xử lý**

## Endpoint đã có sẵn trong dự án

Project hiện đã có sẵn:

- `GET /api/messenger/webhook`
- `POST /api/messenger/webhook`

Ví dụ local:

- `http://localhost:5088/api/messenger/webhook`

Lưu ý rất quan trọng:

- Meta không gọi trực tiếp được `localhost`
- Callback URL bắt buộc phải là HTTPS public

Ví dụ đúng:

- `https://your-domain.com/api/messenger/webhook`
- `https://abc123.ngrok-free.app/api/messenger/webhook`

## Cấu hình trong `appsettings.json`

```json
"Messenger": {
  "Enabled": true,
  "VerifyToken": "my_secure_verify_token_123",
  "PageAccessToken": "",
  "AppSecret": "",
  "DefaultHotelId": "default"
}
```

Ý nghĩa:

- `Enabled`
  - bật chức năng webhook Messenger
- `VerifyToken`
  - chuỗi bạn tự đặt
  - Meta sẽ gửi lại chuỗi này khi verify webhook
  - phải nhập chính xác giống 100% ở Meta Dashboard
- `PageAccessToken`
  - dùng cho bước sau nếu muốn gửi tin nhắn trả về Messenger
- `AppSecret`
  - dùng cho bước sau nếu muốn verify chữ ký request `X-Hub-Signature-256`
- `DefaultHotelId`
  - hotel mặc định để map vào chatbot nếu sau này chưa có logic map Page riêng

## Tổng quan luồng kết nối

Luồng đầy đủ sẽ là:

1. Bạn có sẵn một Meta App trung tâm
2. Bạn thêm Messenger Product vào app đó
3. Khách hàng cấp quyền Facebook Page cho bạn
4. Bạn gắn Facebook Page đó vào app
5. Bạn public WebAPI ra internet bằng HTTPS
6. Bạn nhập callback URL + verify token vào Meta
7. Meta gọi `GET /api/messenger/webhook` để verify
8. Nếu verify thành công, bạn subscribe field `messages`
9. Khi người dùng nhắn tin vào Page, Meta gửi `POST /api/messenger/webhook`

Nếu bạn đang setup lần đầu, hãy làm đúng thứ tự đó. Nếu làm lệch thứ tự, Meta thường báo lỗi verify hoặc không bắn event.

## Quy trình onboarding khách hàng theo hướng thủ công

Đây là flow bán hàng / vận hành nên dùng ở giai đoạn hiện tại.

### Bước 1. Thu thông tin từ khách hàng

Bạn chỉ cần yêu cầu khách hàng:

1. tên Facebook Page cần kết nối
2. link Facebook Page
3. xác nhận ai là admin hiện tại của Page
4. thêm tài khoản phù hợp của bạn vào Page với quyền đủ dùng

Mục tiêu là khách hàng chỉ cần làm phần rất đơn giản:

- cấp quyền Page

Khách hàng không cần:

- tạo app
- thêm product Messenger
- cấu hình callback URL
- tự verify webhook

### Bước 2. Bạn dùng Meta App trung tâm của hệ thống

Với mỗi khách sạn mới:

1. không tạo app mới
2. không tạo webhook mới
3. không đổi endpoint webhook

Bạn chỉ dùng:

- Meta App trung tâm
- WebAPI hiện tại
- callback URL hiện tại

Điều này giúp:

- setup nhanh
- ít lỗi
- khách hàng không bị quá tải thao tác kỹ thuật

## Quy trình cấu hình trên Meta

Phần này là phần quan trọng nhất. Hãy làm lần lượt.

### Bước 1. Chuẩn bị trước khi vào Meta

Trước khi vào Meta, bạn nên chuẩn bị sẵn:

1. WebAPI đã chạy được local
2. Bạn đã biết URL webhook của mình là:
   - `/api/messenger/webhook`
3. Bạn đã chọn một verify token rõ ràng, ví dụ:
   - `hotel-ai-messenger-verify-2026`
4. Bạn có một Facebook Page thật để test
5. Tài khoản Facebook của bạn đã được khách hàng cấp quyền phù hợp trên Page đó

Mình khuyên điền trước vào `appsettings.json`:

```json
"Messenger": {
  "Enabled": true,
  "VerifyToken": "hotel-ai-messenger-verify-2026",
  "PageAccessToken": "",
  "AppSecret": "",
  "DefaultHotelId": "default"
}
```

### Bước 2. Tạo Meta App trung tâm

Nếu bạn đã có Meta App trung tâm rồi thì **bỏ qua bước này**.

Bạn chỉ cần tạo 1 lần cho toàn hệ thống, không tạo mới cho từng khách sạn.

1. Vào Meta for Developers
2. Chọn `My Apps`
3. Chọn `Create App`
4. Chọn loại app phù hợp với Messenger workflow hiện tại
   - nếu Meta UI hỏi use case, hãy chọn loại gần với:
     - `Business`
     - hoặc use case có Messenger / Business Messaging
5. Đặt tên app
6. Chọn Business Account nếu Meta yêu cầu
7. Tạo app

Sau khi tạo xong, bạn sẽ có:

- `App ID`
- `App Secret`

Bạn chưa cần dùng `App Secret` ngay để verify webhook cơ bản, nhưng nên lưu lại.

Khuyến nghị:

- chỉ duy trì 1 app trung tâm ở giai đoạn đầu
- không tách mỗi khách sạn thành 1 app riêng
- webhook vẫn dùng chung một endpoint

Docs:

- [Messenger Platform](https://developers.facebook.com/docs/messenger-platform/)
- [Messenger Getting Started](https://developers.facebook.com/docs/messenger-platform/getting-started)

### Bước 3. Thêm product Messenger

Trong dashboard của app:

1. Tìm mục `Add Product`
2. Chọn `Messenger`
3. Nhấn `Set Up`

Sau khi thêm xong, bạn sẽ thấy các phần kiểu:

- Messenger settings
- Webhooks
- Access Tokens

Nếu chưa thấy Messenger, thường là do:

- tạo sai loại app
- Meta đang đổi UI theo tài khoản/business

Khi đó hãy tìm trong:

- `Use cases`
- `Products`
- hoặc `Messenger API setup`

### Bước 4. Gắn Facebook Page của khách hàng vào app

Messenger bot luôn gắn với một Facebook Page, không gắn trực tiếp với profile cá nhân.

Bạn cần:

1. Vào phần Messenger settings trong app
2. Tìm mục chọn Page
3. Chọn đúng Facebook Page của khách sạn muốn chatbot hoạt động
4. Xác nhận quyền

Sau khi gắn page xong:

- app mới có thể subscribe webhook cho page
- app mới có thể generate Page Access Token

Nếu bạn không thấy Page trong danh sách:

- tài khoản hiện tại chưa được khách hàng cấp quyền đủ
- hoặc page đang thuộc business khác chưa share quyền

Thực tế vận hành:

- đây là chỗ khách hàng thường hỗ trợ bạn bằng cách thêm bạn làm admin/editor page
- sau khi quyền đã đúng, phần còn lại bạn tự xử lý

### Bước 5. Generate Page Access Token cho Page khách sạn

Sau khi page đã được gắn:

1. Trong Messenger settings, tìm phần `Access Tokens`
2. Chọn đúng Page
3. Nhấn `Generate Token`
4. Copy token
5. Lưu token vào:

```json
"Messenger": {
  "PageAccessToken": "YOUR_PAGE_ACCESS_TOKEN"
}
```

Ở bước hiện tại, token này chưa bắt buộc để verify webhook, nhưng sẽ cần ngay khi bạn muốn gửi tin nhắn trả lời lại.

Lưu ý vận hành:

- mỗi Page sẽ có access token riêng
- khi bạn onboard nhiều khách sạn, cần lưu token theo từng Page
- về sau nên lưu mapping:
  - `pageId -> hotelId`
  - `pageId -> pageAccessToken`

### Bước 6. Public WebAPI ra Internet bằng HTTPS

Meta không gọi được `localhost`.

Bạn cần một HTTPS public URL.

#### Cách nhanh nhất để test: `ngrok`

Ví dụ app đang chạy ở cổng `5088`:

```bash
ngrok http 5088
```

Bạn sẽ nhận được URL như:

```text
https://abc123.ngrok-free.app
```

Callback URL thực tế sẽ là:

```text
https://abc123.ngrok-free.app/api/messenger/webhook
```

#### Một số yêu cầu bắt buộc của callback URL

- phải là `https`
- phải public từ internet
- không có redirect vòng
- route phải đúng chính xác `/api/messenger/webhook`

Nếu bạn dùng production domain, cũng áp dụng y như vậy:

```text
https://api.yourdomain.com/api/messenger/webhook
```

### Bước 7. Verify webhook trong Meta

Đây là bước Meta gọi API của bạn để kiểm tra callback URL có hợp lệ không.

Trong phần `Webhooks` của Messenger:

1. Chọn `Add Callback URL` hoặc `Edit Callback URL`
2. Nhập:

Callback URL:

```text
https://abc123.ngrok-free.app/api/messenger/webhook
```

Verify Token:

```text
hotel-ai-messenger-verify-2026
```

Lưu ý:

- `Verify Token` này không phải token do Meta cấp
- đây là chuỗi bạn tự đặt trong config app
- Meta chỉ dùng nó để gọi verify request

#### Meta sẽ gọi request gì

Meta sẽ gọi một request dạng:

```text
GET /api/messenger/webhook?hub.mode=subscribe&hub.verify_token=hotel-ai-messenger-verify-2026&hub.challenge=123456789
```

API của bạn phải:

- kiểm tra `hub.mode == subscribe`
- kiểm tra `hub.verify_token` khớp config
- trả nguyên `hub.challenge`

Nếu đúng, Meta mới cho lưu callback URL.

### Bước 8. Subscribe webhook fields

Sau khi callback URL verify thành công, bạn phải subscribe các field cần nhận.

Ít nhất cho chatbot text cơ bản:

- `messages`

Nên bật thêm:

- `messaging_postbacks`
- `message_deliveries`
- `message_reads`

Giải thích nhanh:

- `messages`
  - bắt buộc để nhận tin nhắn người dùng gửi vào Page
- `messaging_postbacks`
  - hữu ích nếu sau này dùng button / menu
- `message_deliveries`
  - biết tin nhắn đã được giao
- `message_reads`
  - biết người dùng đã đọc

Cho mục tiêu hiện tại, field quan trọng nhất vẫn là:

- `messages`

### Bước 9. Subscribe app vào chính Page đó

Nhiều người verify callback xong nhưng vẫn không nhận được event vì quên subscribe app vào page.

Bạn cần kiểm tra trong Messenger settings:

- app đã thực sự subscribe vào Page đó chưa

Nếu Meta UI có nút kiểu:

- `Subscribe`
- `Add Subscriptions`
- `Connect Page`

hãy đảm bảo page đang ở trạng thái đã subscribe.

Nếu thiếu bước này, webhook có thể verify thành công nhưng `POST` không bắn về API.

### Bước 10. Test nhắn tin thật

Sau khi hoàn thành các bước trên:

1. Dùng Facebook account được phép test
2. Nhắn tin vào Facebook Page
3. Kiểm tra log API

Hiện tại code sẽ:

- nhận webhook POST
- parse các event `messaging`
- log ra:
  - `pageId`
  - `senderId`
  - `recipientId`
  - `messageId`
  - `text`

Nếu thấy log này, nghĩa là kết nối Meta -> WebAPI đã thành công.

## Cách verify webhook hoạt động

### Test verify GET thủ công

Bạn có thể test trước bằng tay để chắc API verify đúng.

Ví dụ local:

```text
GET http://localhost:5088/api/messenger/webhook?hub.mode=subscribe&hub.verify_token=hotel-ai-messenger-verify-2026&hub.challenge=123456
```

Kỳ vọng:

- status `200`
- body là:

```text
123456
```

Nếu body không đúng, Meta sẽ báo verify fail.

### Test POST thủ công

Ví dụ payload giả lập:

```json
{
  "object": "page",
  "entry": [
    {
      "id": "PAGE_ID",
      "time": 1774770000000,
      "messaging": [
        {
          "sender": { "id": "USER_PSID" },
          "recipient": { "id": "PAGE_ID" },
          "timestamp": 1774770000000,
          "message": {
            "mid": "mid.test",
            "text": "Xin chào khách sạn"
          }
        }
      ]
    }
  ]
}
```

Gửi vào:

```text
POST http://localhost:5088/api/messenger/webhook
Content-Type: application/json
```

Kỳ vọng:

- HTTP `200 OK`
- body:

```text
EVENT_RECEIVED
```

- log server sẽ có text message

## Code hiện đang hoạt động thế nào

### `GET /api/messenger/webhook`

Chức năng:

- nhận request verify từ Meta
- đọc:
  - `hub.mode`
  - `hub.verify_token`
  - `hub.challenge`
- nếu token khớp thì trả lại `hub.challenge`

File:

- [MessengerController.cs](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/src/HotelChatbot.API/Controllers/MessengerController.cs)

### `POST /api/messenger/webhook`

Chức năng:

- nhận raw payload từ Meta
- kiểm tra `object == "page"`
- duyệt `entry[].messaging[]`
- parse tin nhắn cơ bản
- log lại thông tin event

File:

- [MessengerController.cs](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/src/HotelChatbot.API/Controllers/MessengerController.cs)
- [MessengerWebhookService.cs](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/src/HotelChatbot.Infrastructure/Services/MessengerWebhookService.cs)

Hiện trạng:

- verify callback: đã có
- nhận webhook POST: đã có
- parse message cơ bản: đã có
- gửi reply ngược Messenger: chưa có

## Checklist cấu hình thành công

Bạn có thể dùng checklist này để rà nhanh:

- Meta App trung tâm đã tạo
- Messenger Product đã thêm
- Facebook Page của khách hàng đã gắn vào app
- `Messenger:Enabled = true`
- `Messenger:VerifyToken` đã cấu hình
- WebAPI đang chạy
- Có HTTPS public URL
- Callback URL nhập đúng `/api/messenger/webhook`
- Verify token nhập đúng y hệt config
- Webhook field `messages` đã subscribe
- App đã subscribe vào đúng Page
- Nhắn thử vào Page và API nhận được `POST`

## Các lỗi thường gặp khi verify callback

### 1. Callback URL dùng localhost

Sai:

```text
http://localhost:5088/api/messenger/webhook
```

Meta không gọi được URL này.

Cách xử lý:

- dùng `ngrok`
- hoặc deploy ra domain thật

### 2. Verify token không khớp

Biểu hiện:

- Meta báo verify thất bại
- API trả `403`

Cách xử lý:

- đối chiếu lại `Messenger:VerifyToken`
- đảm bảo không thừa khoảng trắng
- đảm bảo copy đúng chính xác chữ hoa/chữ thường nếu bạn muốn strict match

### 3. Callback URL sai route

Sai:

- `/api/messenger`
- `/messenger/webhook`
- `/api/messenger/webhooks`

Đúng:

- `/api/messenger/webhook`

### 4. App verify xong nhưng không có POST event

Nguyên nhân thường là:

- chưa subscribe field `messages`
- app chưa subscribe vào Page
- đang test bằng user không phù hợp với app mode / role hiện tại

### 5. HTTPS URL hết hạn

Hay gặp khi dùng `ngrok` free.

Biểu hiện:

- verify trước đó thành công
- nhưng vài giờ sau không nhận webhook nữa

Cách xử lý:

- lấy URL tunnel mới
- update lại callback URL trong Meta

## Bước tiếp theo sau khi webhook đã kết nối

Sau khi xác nhận Meta đã POST vào WebAPI thành công, bước tiếp theo nên làm là:

1. lưu mapping `pageId -> hotelId`
2. lưu token hoặc cấu hình liên quan theo từng page
3. map `sender.id` của Messenger thành `sessionId`
4. lấy `message.text`
5. gọi `IConversationService.ProcessMessageAsync(...)`
6. lấy response chatbot
7. gửi reply về Messenger qua Send API

Docs:

- [Messenger Send API](https://developers.facebook.com/docs/messenger-platform/send-messages)

## Lộ trình tương lai: Hướng 2 self-service

Hiện tại tài liệu này đang tối ưu cho:

- **Hướng 1: bạn setup thủ công cho khách hàng**

Đây là hướng phù hợp nhất để:

- ra mắt nhanh
- giảm friction khi bán hàng
- tránh khách hàng phải vào Meta Developer

Tương lai, khi sản phẩm ổn định hơn, bạn có thể nâng cấp sang:

- **Hướng 2: khách hàng tự kết nối Page qua flow self-service / OAuth**

Khi đó trải nghiệm sẽ là:

1. khách hàng đăng nhập Facebook từ hệ thống của bạn
2. khách hàng chọn Page muốn kết nối
3. khách hàng bấm cấp quyền
4. hệ thống tự lưu token và mapping page

Nhưng ở giai đoạn hiện tại, chưa nên bắt khách đi theo flow đó nếu mục tiêu là chốt deal nhanh.

## Khuyến nghị triển khai thực tế

Nếu bạn đang bán cho khách sạn tại Việt Nam, cách nói dễ chốt hơn là:

- “Anh/chị chỉ cần cấp quyền fanpage, bên em cấu hình toàn bộ phần kỹ thuật.”

Không nên yêu cầu khách:

- tự tạo Meta App
- tự cấu hình webhook
- tự debug verify token

Điều này đúng đặc biệt với:

- boutique hotel
- homestay cao cấp
- resort nhỏ
- khách sạn chưa có đội kỹ thuật riêng

## Checklist onboarding cho mỗi khách sạn mới

Mỗi khi onboard 1 khách sạn mới, bạn chỉ cần đi theo checklist sau:

1. nhận link Facebook Page
2. được cấp quyền phù hợp vào Page
3. gắn Page vào Meta App trung tâm
4. generate Page Access Token
5. verify page đã subscribe vào webhook
6. test nhắn tin thật vào Page
7. kiểm tra webhook POST vào API
8. lưu mapping `pageId -> hotelId`
9. lưu token của page đó
10. chuyển sang bước tích hợp chatbot reply

## Khuyến nghị vận hành

- Local dev: dùng `ngrok` hoặc `cloudflared`
- Production: dùng domain thật + HTTPS
- Nên thêm verify `X-Hub-Signature-256` ở bước tiếp theo
- Nên trả `200 OK` nhanh, tránh xử lý webhook quá lâu
- Nên log riêng:
  - `pageId`
  - `senderId`
  - `messageId`
  - `timestamp`
  - `text`

## Nguồn chính thức

- [Messenger Platform](https://developers.facebook.com/docs/messenger-platform/)
- [Messenger Getting Started](https://developers.facebook.com/docs/messenger-platform/getting-started)
- [Messenger Webhooks](https://developers.facebook.com/docs/messenger-platform/webhooks)
- [Graph API Webhooks Getting Started](https://developers.facebook.com/docs/graph-api/webhooks/getting-started)
- [Messenger Send API](https://developers.facebook.com/docs/messenger-platform/send-messages)
