# API Documentation

## Overview

`ChatHotelAIBackend` là .NET WebAPI cho chatbot lễ tân khách sạn.

Hệ thống hiện hỗ trợ:
- Chatbot tư vấn khách sạn
- Kiểm tra phòng trống
- Tạo booking trực tiếp
- Tra cứu booking
- Hủy booking
- Nguồn dữ liệu từ Google Sheets
- AI provider có thể chọn giữa `Claude` và `OpenAI` bằng cấu hình

Base URL khi chạy local theo `launchSettings.json`:
- `http://localhost:5088`

Swagger:
- `http://localhost:5088/swagger`

Health check:
- `GET /health`

## Architecture

Luồng chính:
1. API nhận request từ client.
2. `ConversationService` xử lý hội thoại và intent.
3. `IHotelAIService` gọi provider AI đang active.
4. `GoogleSheetsService` đọc dữ liệu khách sạn, phòng, giá, FAQ, khuyến mãi.
5. `BookingService` ghi booking và cập nhật availability.

AI provider hiện có:
- `Claude` qua `ClaudeAIService`
- `OpenAI` qua `OpenAIService`

Provider active được chọn bằng:
- `AIProvider:Provider`

## Configuration

File cấu hình chính:
- [appsettings.json](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/src/HotelChatbot.API/appsettings.json)

### Google Sheets

```json
"GoogleSheets": {
  "SpreadsheetId": "YOUR_GOOGLE_SPREADSHEET_ID_HERE",
  "CredentialsPath": "credentials/google-service-account.json",
  "CredentialsJson": "",
  "CacheMinutes": 5
}
```

### AI Provider

```json
"AIProvider": {
  "Provider": "Claude"
}
```

Giá trị hợp lệ:
- `Claude`
- `OpenAI`

### Claude

```json
"ClaudeAI": {
  "ApiKey": "YOUR_ANTHROPIC_API_KEY_HERE",
  "Model": "claude-opus-4-5",
  "MaxTokens": 1024,
  "Temperature": 0.7
}
```

### OpenAI

```json
"OpenAI": {
  "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
  "Model": "gpt-4.1-mini",
  "MaxOutputTokens": 1024,
  "Temperature": 0.7
}
```

Để chuyển sang OpenAI:

```json
"AIProvider": {
  "Provider": "OpenAI"
}
```

## Seed Data

Project đã có seed data tại:
- [seed](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/seed)

File chính:
- [InterContinental_Nha_Trang_Google_Sheets_Seed.xlsx](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/seed/InterContinental_Nha_Trang_Google_Sheets_Seed.xlsx)

Bạn có thể import file này vào Google Sheets theo hướng dẫn:
- [GOOGLE_SHEETS_GUIDE.md](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/docs/GOOGLE_SHEETS_GUIDE.md)

## Endpoints

## 1. Health Check

### `GET /health`

Kiểm tra trạng thái service.

Response mẫu:

```json
{
  "status": "healthy",
  "timestamp": "2026-03-29T03:32:21.9509Z",
  "version": "1.0.0",
  "service": "Hotel AI Chatbot API"
}
```

## 2. Root Redirect

### `GET /`

Redirect về Swagger UI.

## 3. Chat

Base route:
- `api/chat`

### `POST /api/chat/message`

Gửi tin nhắn tới chatbot AI lễ tân.

Request body:

```json
{
  "sessionId": "session-001",
  "message": "Tôi muốn đặt phòng 2 người vào cuối tuần này",
  "hotelId": "intercontinental_nhatrang",
  "language": "vi"
}
```

Field:
- `sessionId`: id phiên chat, có thể để rỗng để hệ thống tự tạo
- `message`: nội dung khách gửi
- `hotelId`: mã khách sạn
- `language`: ngôn ngữ, hiện đang dùng `vi` hoặc `en`

Response mẫu:

```json
{
  "sessionId": "session-001",
  "message": "Dạ anh/chị cho em xin ngày check-in và check-out mong muốn nhé.",
  "intent": "booking_init",
  "bookingDraft": {
    "guestName": null,
    "guestPhone": null,
    "guestEmail": null,
    "checkInDate": null,
    "checkOutDate": null,
    "numAdults": null,
    "numChildren": null,
    "roomType": null,
    "roomId": null,
    "specialRequests": null,
    "promoCode": null,
    "estimatedTotal": null,
    "completionPercentage": "0%"
  },
  "roomSuggestions": null,
  "isBookingComplete": false,
  "bookingId": null,
  "timestamp": "2026-03-29T03:30:00Z"
}
```

Lỗi thường gặp:
- `400 Bad Request`: thiếu `message`
- `500 Internal Server Error`: lỗi AI provider hoặc lỗi xử lý nội bộ

### `POST /api/chat/session`

Tạo session chat mới.

Query:
- `hotelId` mặc định là `default`

Ví dụ:
- `POST /api/chat/session?hotelId=intercontinental_nhatrang`

Response mẫu:

```json
{
  "sessionId": "8cbe10b8-f9d2-4950-95f2-31dfca1d2217",
  "hotelId": "intercontinental_nhatrang",
  "createdAt": "2026-03-29T03:00:00Z"
}
```

## 4. Booking

Base route:
- `api/booking`

### `POST /api/booking`

Tạo booking trực tiếp, không qua chat.

Request body:

```json
{
  "sessionId": "session-001",
  "hotelId": "intercontinental_nhatrang",
  "guestName": "Nguyen Van A",
  "guestPhone": "0901234567",
  "guestEmail": "a@example.com",
  "guestIdCard": "079123456789",
  "roomId": "ICNH-0801",
  "checkInDate": "2026-04-12T00:00:00",
  "checkOutDate": "2026-04-14T00:00:00",
  "numAdults": 2,
  "numChildren": 0,
  "specialRequests": "Tang cao neu co",
  "promoCode": "",
  "breakfastIncluded": false,
  "paymentMethod": "Cash"
}
```

Response mẫu:

```json
{
  "bookingId": "BK2603291234",
  "guestName": "Nguyen Van A",
  "roomTypeName": "Classic Ocean View King",
  "roomNumber": "801",
  "checkInDate": "2026-04-12T00:00:00",
  "checkOutDate": "2026-04-14T00:00:00",
  "totalNights": 2,
  "numAdults": 2,
  "numChildren": 0,
  "roomRate": 2950000,
  "discountAmount": 0,
  "finalAmount": 5900000,
  "breakfastIncluded": false,
  "paymentMethod": "Cash",
  "checkInTime": "15:00",
  "checkOutTime": "12:00",
  "hotelPhone": "+84 258 388 7777",
  "hotelAddress": "32-34 Tran Phu Street, Nha Trang Ward, Khanh Hoa Province, 300202, Vietnam",
  "confirmationMessage": "..."
}
```

Status codes:
- `200 OK`: tạo booking thành công
- `400 Bad Request`: dữ liệu không hợp lệ
- `409 Conflict`: phòng không còn trống hoặc không thể tạo booking
- `500 Internal Server Error`: lỗi hệ thống

### `GET /api/booking/{bookingId}`

Tra cứu booking theo mã.

Ví dụ:
- `GET /api/booking/BK2603291234`

Response:
- Trả về object booking nếu tồn tại
- `404 Not Found` nếu không tìm thấy

### `DELETE /api/booking/{bookingId}`

Hủy booking.

Query:
- `reason` mặc định là `Khách yêu cầu hủy`

Ví dụ:
- `DELETE /api/booking/BK2603291234?reason=Thay%20doi%20lich%20trinh`

Response mẫu:

```json
{
  "message": "Đã hủy đặt phòng thành công",
  "bookingId": "BK2603291234"
}
```

## 5. Availability

Base route:
- `api/availability`

### `GET /api/availability`

Kiểm tra phòng trống theo ngày và số khách.

Query parameters:
- `hotelId`
- `checkInDate`
- `checkOutDate`
- `numAdults`
- `numChildren`
- `preferredRoomType`

Ví dụ:

```text
GET /api/availability?hotelId=intercontinental_nhatrang&checkInDate=2026-04-12&checkOutDate=2026-04-14&numAdults=2&numChildren=0
```

Response mẫu:

```json
{
  "checkInDate": "2026-04-12T00:00:00",
  "checkOutDate": "2026-04-14T00:00:00",
  "totalNights": 2,
  "availableRooms": [
    {
      "roomId": "ICNH-0801",
      "roomNumber": "801",
      "roomType": "Classic",
      "roomTypeName": "Classic Ocean View King",
      "description": "Phong Classic Ocean View 41m2 voi giuong king...",
      "shortDescription": "41m2, giuong king, huong bien",
      "sizeM2": 41,
      "maxOccupancy": 3,
      "bedType": "1 King Bed",
      "view": "Ocean View",
      "pricePerNight": 2950000,
      "totalPrice": 5900000,
      "amenities": [
        "Wifi mien phi",
        "Dieu hoa",
        "TV"
      ],
      "breakfastIncluded": false
    }
  ],
  "hasAvailability": true
}
```

Validation:
- Nếu `checkInDate >= checkOutDate` sẽ trả `400 Bad Request`

## 6. Hotel Info

Base route:
- `api/hotel`

### `GET /api/hotel/{hotelId}/info`

Lấy thông tin tổng quan khách sạn.

Ví dụ:
- `GET /api/hotel/intercontinental_nhatrang/info`

### `GET /api/hotel/{hotelId}/promotions`

Lấy danh sách khuyến mãi đang áp dụng.

Query optional:
- `roomType`
- `checkIn`
- `checkOut`

Ví dụ:
- `GET /api/hotel/intercontinental_nhatrang/promotions?roomType=Classic&checkIn=2026-04-12&checkOut=2026-04-14`

## DTO Summary

### ChatRequest

```json
{
  "sessionId": "string",
  "message": "string",
  "hotelId": "string",
  "language": "vi"
}
```

### CreateBookingRequest

```json
{
  "sessionId": "string",
  "hotelId": "string",
  "guestName": "string",
  "guestPhone": "string",
  "guestEmail": "string",
  "guestIdCard": "string",
  "roomId": "string",
  "checkInDate": "datetime",
  "checkOutDate": "datetime",
  "numAdults": 2,
  "numChildren": 0,
  "specialRequests": "string",
  "promoCode": "string",
  "breakfastIncluded": false,
  "paymentMethod": "Cash"
}
```

### AvailabilityRequest

```json
{
  "hotelId": "string",
  "checkInDate": "datetime",
  "checkOutDate": "datetime",
  "numAdults": 2,
  "numChildren": 0,
  "preferredRoomType": "string"
}
```

## Running Locally

### Build

```bash
dotnet build ChatHotel.sln
```

### Run

```bash
dotnet run --project src/HotelChatbot.API/HotelChatbot.API.csproj
```

### Verify

```bash
curl http://localhost:5088/health
```

## Notes

- Hệ thống hiện không dùng vector database.
- Dữ liệu realtime đang đến từ Google Sheets.
- `OpenAI` đã được tích hợp theo kiểu provider-based, nhưng mặc định vẫn là `Claude`.
- Nếu chưa có API key hoặc Google Sheets credentials hợp lệ, các endpoint phụ thuộc AI/data source có thể không hoạt động đúng.
