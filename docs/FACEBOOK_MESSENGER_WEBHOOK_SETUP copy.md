# Facebook Messenger Webhook Setup

Updated: 2026-03-29

## Mục tiêu

Tài liệu này giúp bạn:

1. Cấu hình Meta App và Messenger webhook
2. Verify webhook với WebAPI của dự án
3. Nhận được webhook POST từ Facebook Messenger vào API

## Endpoint đã có trong project

- `GET /api/messenger/webhook`
- `POST /api/messenger/webhook`

Ví dụ local:

- `http://localhost:5088/api/messenger/webhook`

Lưu ý quan trọng:

- Meta **không gọi được localhost trực tiếp**
- Bạn cần public WebAPI bằng domain HTTPS công khai, ví dụ:
  - `https://your-domain.com/api/messenger/webhook`
  - hoặc tunnel tạm như `ngrok`, `cloudflared`

## Cấu hình appsettings.json

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

- `Enabled`: bật webhook Messenger
- `VerifyToken`: chuỗi bạn tự đặt, phải nhập đúng y hệt bên Meta App Dashboard
- `PageAccessToken`: để bước sau gửi reply qua Send API
- `AppSecret`: để bước sau verify signature
- `DefaultHotelId`: hotel mặc định nếu sau này map page vào hotel

## Quy trình cấu hình trên Meta

### 1. Tạo App

1. Vào Meta for Developers
2. Tạo App mới
3. Thêm product `Messenger`

Official docs:

- https://developers.facebook.com/docs/messenger-platform/
- https://developers.facebook.com/docs/messenger-platform/getting-started

### 2. Kết nối Facebook Page

1. Chọn Facebook Page mà bot sẽ hoạt động
2. Generate Page Access Token cho page đó
3. Lưu token này vào `Messenger:PageAccessToken`

Official docs:

- https://developers.facebook.com/docs/messenger-platform/getting-started/app-setup

### 3. Public WebAPI ra Internet

Ví dụ với `ngrok`:

```bash
ngrok http 5088
```

Sau đó bạn sẽ có URL kiểu:

```text
https://abc123.ngrok-free.app
```

Webhook callback URL sẽ là:

```text
https://abc123.ngrok-free.app/api/messenger/webhook
```

### 4. Cấu hình Webhooks trong Meta

Trong Messenger settings / Webhooks:

- Callback URL:
  - `https://abc123.ngrok-free.app/api/messenger/webhook`
- Verify Token:
  - chính là giá trị `Messenger:VerifyToken`

Meta sẽ gửi request GET dạng:

```text
GET /api/messenger/webhook?hub.mode=subscribe&hub.verify_token=...&hub.challenge=...
```

Nếu token khớp, API sẽ trả lại nguyên `hub.challenge`.

### 5. Subscribe webhook fields

Ít nhất nên subscribe:

- `messages`
- `messaging_postbacks`
- `message_deliveries`
- `message_reads`

Cho bước đầu kết nối chatbot, field quan trọng nhất là:

- `messages`

Official docs:

- https://developers.facebook.com/docs/graph-api/webhooks/getting-started
- https://developers.facebook.com/docs/messenger-platform/webhooks

## Cách verify webhook hoạt động

### Verify GET

Bạn có thể test local trước bằng tay:

```text
GET http://localhost:5088/api/messenger/webhook?hub.mode=subscribe&hub.verify_token=my_secure_verify_token_123&hub.challenge=123456
```

Kỳ vọng:

- response body: `123456`

### Test POST

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
- response: `EVENT_RECEIVED`
- log server sẽ ghi:
  - `pageId`
  - `senderId`
  - `recipientId`
  - `messageId`
  - `text`

## Cách code hiện đang hoạt động

### GET `/api/messenger/webhook`

Chức năng:

- nhận request verify từ Meta
- so sánh `hub.verify_token` với `Messenger:VerifyToken`
- nếu đúng thì trả về `hub.challenge`

### POST `/api/messenger/webhook`

Chức năng:

- nhận payload Messenger webhook
- chỉ xử lý object = `page`
- duyệt qua `entry[].messaging[]`
- parse message cơ bản
- log lại thông tin event

Hiện tại webhook receiver đã sẵn sàng cho:

- verify callback
- nhận message event
- kết nối Meta vào WebAPI thành công

Hiện tại **chưa gửi reply lại Messenger**. Đây là bước tiếp theo.

## Những gì bạn cần chuẩn bị để kết nối thành công

Checklist:

- Meta App đã tạo
- Product Messenger đã thêm
- Facebook Page đã kết nối
- `Messenger:Enabled = true`
- `Messenger:VerifyToken` đã cấu hình
- WebAPI có public HTTPS URL
- Callback URL nhập đúng `/api/messenger/webhook`
- Webhook field `messages` đã subscribe

## Bước tiếp theo sau khi webhook đã kết nối

Sau khi xác nhận webhook POST vào được API, bước tiếp theo là:

1. map `sender.id` của Messenger thành `sessionId`
2. gọi `IConversationService.ProcessMessageAsync(...)`
3. lấy response chatbot
4. gửi lại khách qua Messenger Send API

Official docs cho gửi tin nhắn:

- https://developers.facebook.com/docs/messenger-platform/send-messages

## Lưu ý vận hành

- Với local development, nên dùng tunnel HTTPS
- Với production, nên dùng domain thật + HTTPS
- Nên thêm verify chữ ký `X-Hub-Signature-256` ở bước tiếp theo để tăng bảo mật
- Nên tránh xử lý lâu trong webhook; Meta chỉ cần nhận `200 OK` nhanh
