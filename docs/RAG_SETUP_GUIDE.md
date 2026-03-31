# RAG Setup Guide

## Mục tiêu

Tài liệu này hướng dẫn cách bật RAG cho `ChatHotelAIBackend` từ trạng thái:
- app đang chạy bình thường
- chưa có dữ liệu trong Qdrant

đến trạng thái:
- dữ liệu khách sạn đã được index
- chatbot có thể retrieve knowledge từ Qdrant

## 1. Trạng thái mặc định

Mặc định trong [appsettings.json](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/src/HotelChatbot.API/appsettings.json):

```json
"RAG": {
  "Enabled": false
}
```

Khi đó:
- app vẫn chạy như cũ
- không dùng Qdrant
- không cần embeddings
- không ảnh hưởng booking flow

## 2. Chuẩn bị dữ liệu nguồn

Đảm bảo bạn đã có:
- Google Sheets chứa dữ liệu khách sạn
- `SpreadsheetId`
- service account credentials hợp lệ

Seed data mẫu:
- [seed](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/seed)

Guide tạo Google Sheets:
- [GOOGLE_SHEETS_GUIDE.md](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/docs/GOOGLE_SHEETS_GUIDE.md)

## 3. Chạy Qdrant local bằng Docker

Nếu máy đã có Docker:

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

Sau khi chạy, Qdrant thường sẵn sàng tại:
- `http://localhost:6333`

## 4. Cấu hình appsettings.json

Cập nhật các section sau trong [appsettings.json](/Users/mrsteve.bang/Documents/_projects/chatbot-ai-sample/ChatHotelAIBackend/src/HotelChatbot.API/appsettings.json):

### Google Sheets

```json
"GoogleSheets": {
  "SpreadsheetId": "YOUR_SPREADSHEET_ID",
  "CredentialsPath": "credentials/google-service-account.json",
  "CredentialsJson": "",
  "CacheMinutes": 5
}
```

### OpenAI Embedding

```json
"OpenAIEmbedding": {
  "ApiKey": "YOUR_OPENAI_API_KEY",
  "Model": "text-embedding-3-small"
}
```

### Qdrant

```json
"Qdrant": {
  "BaseUrl": "http://localhost:6333",
  "ApiKey": "",
  "CollectionName": "hotel_knowledge_v1",
  "VectorSize": 1536
}
```

### RAG

```json
"RAG": {
  "Enabled": true,
  "TopK": 5,
  "ScoreThreshold": 0.65,
  "UseIntentFilter": true,
  "UseRagForChat": true,
  "ReplaceFullHotelContext": false
}
```

## 5. Khởi động app

```bash
dotnet run --project src/HotelChatbot.API/HotelChatbot.API.csproj
```

Kiểm tra health:

```bash
curl http://localhost:5088/health
```

## 6. Reindex dữ liệu vào Qdrant

Gọi endpoint:

```bash
curl -X POST http://localhost:5088/api/rag/reindex/intercontinental_nhatrang
```

Ví dụ response:

```json
{
  "hotelId": "intercontinental_nhatrang",
  "indexedChunks": 25,
  "indexedAt": "2026-03-29T05:30:00Z"
}
```

Điều này có nghĩa:
- app đã đọc dữ liệu từ Google Sheets
- convert thành knowledge documents
- tạo embeddings
- upsert vào Qdrant collection

## 7. Test retrieval

Dùng endpoint preview:

```bash
curl -X POST http://localhost:5088/api/rag/search \
  -H "Content-Type: application/json" \
  -d '{
    "hotelId": "intercontinental_nhatrang",
    "query": "Khách sạn check-in lúc mấy giờ?",
    "intent": "policy_inquiry"
  }'
```

Nếu thành công, response sẽ có `context` khác rỗng.

## 8. Chat flow sau khi bật RAG

Khi user gửi tin nhắn:
1. app phân tích intent
2. app tạo query embedding từ câu hỏi
3. app search Qdrant theo `hotel_id`
4. app lấy top chunks liên quan
5. app append vào prompt:

```text
=== TRI THỨC LIÊN QUAN TỪ KNOWLEDGE BASE ===
...
```

6. app vẫn lấy dữ liệu động từ Google Sheets/logic code cho:
- availability
- pricing runtime
- booking

## 9. Khi nào cần reindex lại

Bạn nên reindex lại khi:
- sửa FAQ
- sửa thông tin khách sạn
- đổi room description
- đổi amenities
- đổi promotion text

Không bắt buộc reindex ngay khi:
- chỉ có bookings mới
- availability thay đổi

Lý do:
- bookings/availability không phải knowledge tĩnh cho vector DB

## 10. Những gì Qdrant không thay thế

Qdrant không thay thế:
- Google Sheets availability
- Google Sheets bookings
- logic pricing
- logic tạo booking

Qdrant chỉ hỗ trợ:
- semantic retrieval
- giảm token input
- trả lời FAQ/policy/amenity/location/room info tốt hơn

## 11. Troubleshooting

### Reindex trả lỗi

Kiểm tra:
- `OpenAIEmbedding.ApiKey`
- `Qdrant.BaseUrl`
- `SpreadsheetId`
- quyền service account trên Google Sheets

### Search trả context rỗng

Kiểm tra:
- đã bật `RAG.Enabled=true` chưa
- đã gọi `reindex` chưa
- `hotelId` có đúng không
- Google Sheets có dữ liệu không

### App vẫn trả lời như cũ

Có thể do:
- `RAG.Enabled=false`
- retrieval không ra chunk phù hợp
- hoặc knowledge context rỗng

## 12. Quy trình rollout an toàn

Khuyến nghị:
1. deploy code với `RAG.Enabled=false`
2. kiểm tra app chạy ổn
3. cấu hình Qdrant + embeddings
4. bật `RAG.Enabled=true`
5. reindex từng hotel
6. test `/api/rag/search`
7. test chat thực tế

## 13. Lệnh nhanh

### Build

```bash
dotnet build ChatHotel.sln
```

### Run

```bash
dotnet run --project src/HotelChatbot.API/HotelChatbot.API.csproj
```

### Health

```bash
curl http://localhost:5088/health
```

### Reindex

```bash
curl -X POST http://localhost:5088/api/rag/reindex/intercontinental_nhatrang
```

### Search preview

```bash
curl -X POST http://localhost:5088/api/rag/search \
  -H "Content-Type: application/json" \
  -d '{"hotelId":"intercontinental_nhatrang","query":"Khách sạn có hồ bơi không?","intent":"amenity_inquiry"}'
```
