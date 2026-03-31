# CLAUDE.md — ChatHotelAIBackend

Tài liệu này giúp Claude Code hiểu dự án, cách làm việc, và các quy tắc quan trọng khi tham gia vào codebase này.

---

## Dự án là gì

**Hotel AI Chatbot Platform** — backend ASP.NET Core cho một nền tảng chatbot AI chuyên biệt ngành lưu trú tại Việt Nam. Chatbot đóng vai trò "lễ tân AI": trả lời FAQ, tư vấn phòng, kiểm tra availability, tạo booking thật, hỗ trợ hậu-booking.

Mục tiêu thương mại: SaaS bán cho khách sạn nhỏ và vừa, nhà nghỉ, homestay tại Việt Nam. Pricing phải phù hợp thị trường → cost AI phải được kiểm soát chặt.

---

## Tech Stack

- **Runtime:** .NET 9, ASP.NET Core
- **AI Provider:** OpenAI (gpt-4o-mini mặc định) hoặc Claude (swap qua config `AIProvider:Provider`)
- **Embedding:** OpenAI `text-embedding-3-small`
- **Vector DB:** Qdrant (chạy local hoặc cloud)
- **Knowledge Source:** Google Sheets (source of truth cho hotel data)
- **Channel:** Web Chat (REST API) + Facebook Messenger (webhook)
- **Logging:** Serilog (console + file rolling daily)
- **Caching:** IMemoryCache (in-process)

---

## Cấu trúc project

```
ChatHotelAIBackend/
├── src/
│   ├── HotelChatbot.Core/          # Domain models, DTOs, interfaces
│   │   ├── DomainModels.cs         # HotelInfo, Room, Booking, FAQ, ...
│   │   ├── DTOs.cs                 # ChatRequest/Response, ConversationSession, ...
│   │   ├── IServices.cs            # Tất cả interfaces
│   │   └── RagModels.cs            # KnowledgeDocument, KnowledgeChunk, ...
│   │
│   ├── HotelChatbot.Infrastructure/ # Implementations
│   │   ├── AI/
│   │   │   ├── HotelAIServiceBase.cs    # BuildSystemPromptAsync, AnalyzeIntentAsync
│   │   │   ├── OpenAIService.cs         # OpenAI chat completion
│   │   │   └── ClaudeAIService.cs       # Claude chat completion
│   │   ├── GoogleSheets/
│   │   │   └── GoogleSheetsService.cs   # Đọc/ghi tất cả data từ Sheets
│   │   ├── RAG/
│   │   │   ├── Services/
│   │   │   │   ├── RagContextService.cs         # Build knowledge context cho chat
│   │   │   │   ├── KnowledgeIndexingService.cs  # Reindex hotel knowledge vào Qdrant
│   │   │   │   ├── QdrantVectorStoreService.cs  # Upsert/search Qdrant
│   │   │   │   ├── OpenAIEmbeddingService.cs    # Tạo embedding vector
│   │   │   │   ├── HotelKnowledgeDocumentFactory.cs # Build documents từ Sheets
│   │   │   │   └── NoOpRagContextService.cs     # Fallback khi RAG tắt
│   │   └── Services/
│   │       ├── ConversationService.cs   # Orchestrator chính — xử lý mọi message
│   │       ├── BookingService.cs        # Tạo/hủy/tra booking
│   │       ├── HotelDataService.cs      # Availability, pricing, promotions
│   │       ├── MessengerWebhookService.cs # Facebook Messenger webhook
│   │       └── AIUsageService.cs        # Track AI usage và cost
│   │
│   └── HotelChatbot.API/
│       ├── Controllers/
│       │   ├── Controllers.cs          # Chat, Booking, Availability, Hotel
│       │   ├── MessengerController.cs  # Webhook verify + receive
│       │   ├── RagController.cs        # Reindex + search preview
│       │   └── UsageController.cs      # AI usage summary
│       ├── Program.cs                  # DI registration, middleware
│       └── appsettings.json            # Config (KHÔNG commit key thật)
│
├── docs/
│   ├── reports/                    # Code review, analysis reports
│   ├── PRD.md                      # Product Requirements
│   ├── VISON_MISSION.md
│   ├── HOTEL_CHATBOT_PRODUCTION_BLUEPRINT.md
│   ├── IMPLEMENTATION_ROADMAP.md
│   ├── RELEASE_GAP_ANALYSIS.md
│   ├── RAG_QDRANT_DESIGN.md
│   └── ...
└── seed/                           # Seed data cho Google Sheets
```

---

## Nguyên tắc thiết kế cốt lõi

**1. AI không phải source of truth**
AI chỉ làm: hiểu ý khách, trích xuất entity, diễn đạt tự nhiên.
AI không tự quyết: availability, giá, booking status, booking có thành công chưa.
Những thứ này phải lấy từ Google Sheets / business services.

**2. Tách chat state và business state**
- `ConversationSession` = short-term chat context (30 phút)
- `ActiveBookingContext` = business state gắn với booking lifecycle (cần implement)
- Customer memory dài hạn = chưa có, cần làm sau

**3. Channel adapter không làm vỡ workflow lõi**
Messenger, web chat chỉ là adapter. Business logic không phụ thuộc channel.

**4. Tối ưu cost AI**
Thị trường Việt Nam nhạy cảm với giá. Mọi AI call không cần thiết đều là vấn đề.

---

## Flow xử lý message chính

```
ChatRequest
    ↓
ConversationService.ProcessMessageAsync()
    ↓
[1] GetOrCreateSession()
    ↓
[2] AnalyzeIntentAsync()  ← AI call #1 (intent + entity extraction)
    ↓
[3] ApplyDeterministicRelativeDateOverrides()  ← rule-based date parser
    ↓
[4] UpdateBookingDraftFromEntities()
    ↓
[5] BuildSystemPromptAsync()  ← full hotel context từ Sheets
    ↓
[6] BuildKnowledgeContextAsync()  ← RAG retrieval (nếu enabled)
    ↓
[7] BuildAvailabilityContextAsync()  ← lấy phòng trống thực tế (nếu cần)
    ↓
[8] GetChatCompletionAsync()  ← AI call #2 (chat reply)
    ↓
[9] TryCreateBookingIfReady()  ← tạo booking thật nếu đủ điều kiện
    ↓
ChatResponse
```

---

## Các vấn đề đã biết cần giải quyết (đừng bỏ qua)

Xem chi tiết tại `docs/reports/CODE_REVIEW_2026_03_31.md`.

### P0 — Critical bugs trước release

| # | Vấn đề | File |
|---|---|---|
| 1 | Không có hậu-booking state guard — `cảm ơn` sau booking có thể tạo booking lần 2 | `ConversationService.cs:132` |
| 2 | Không có idempotency cho booking creation — race condition tạo double booking | `BookingService.cs:50` |
| 3 | Mọi message đều tốn 2 AI calls kể cả `ok`, `dạ` | `ConversationService.cs:92` |
| 4 | `pageId → hotelId` mapping chưa có — mọi Messenger page dùng chung 1 hotel | `MessengerWebhookService.cs:216` |
| 5 | `new Random()` mỗi lần → booking ID có thể trùng | `BookingService.cs:26` |

### P1 — Cần sớm sau release

| # | Vấn đề | File |
|---|---|---|
| 6 | Không có caching Google Sheets — mỗi message gọi Sheets 5-6 lần | `GoogleSheetsService.cs` |
| 7 | Temporal parser thiếu: `mai`, `mốt`, `cuối tuần`, `DD/MM` | `ConversationService.cs:238` |
| 8 | Full hotel context trong system prompt kể cả khi RAG đã bật — double data | `HotelAIServiceBase.cs:17` |
| 9 | Session chết sau 30 phút, không có Active Booking Context | `DTOs.cs` |
| 10 | Messenger webhook signature chưa được verify | `MessengerWebhookService.cs` |
| 11 | `[BOOKING_READY]` marker từ AI không đáng tin cậy | `HotelAIServiceBase.cs:72` |

---

## Bảo mật — lưu ý quan trọng

- `appsettings.json` KHÔNG được commit key thật (OpenAI key, Messenger token, AppSecret). Dùng `appsettings.Development.json` local hoặc environment variables.
- Hiện tại không có authentication trên `/api/chat/message` — cần thêm trước production.
- CORS đang `AllowAll` — cần restrict trước khi deploy.
- Messenger webhook chưa verify `X-Hub-Signature-256`.

---

## Cách chạy local

```bash
# Cần Qdrant chạy local nếu RAG:Enabled = true
docker run -p 6333:6333 qdrant/qdrant

# Chạy API
cd src/HotelChatbot.API
dotnet run
# Swagger: http://localhost:5062/swagger
```

### Config tối thiểu cần có trong appsettings.Development.json

```json
{
  "GoogleSheets": {
    "SpreadsheetId": "...",
    "CredentialsPath": "credentials/google-service-account.json"
  },
  "OpenAI": {
    "ApiKey": "sk-..."
  },
  "Messenger": {
    "Enabled": false
  },
  "RAG": {
    "Enabled": false
  }
}
```

---

## Quy tắc khi làm việc với codebase này

### Về nghiệp vụ booking
- Không bao giờ để AI tự quyết định booking thành công hay không
- Trước khi thêm bất kỳ intent nào vào booking flow → phải có guard hậu-booking
- Mọi thay đổi `ConversationSession` phải tương thích với booking lifecycle

### Về AI cost
- Không thêm AI call mới nếu có thể dùng rule-based
- Low-value messages (`ok`, `dạ`, `cảm ơn`, `hello`) phải được filter trước khi gọi AI
- Không feed toàn bộ lịch sử chat cũ vào AI

### Về Google Sheets
- Sheets là source of truth cho hotel data và booking
- Khi thêm method mới vào `IGoogleSheetsService` → phải cân nhắc caching
- Availability data phải luôn được lấy fresh (cache ngắn, tối đa 2 phút)

### Về code style
- Interface trước, implementation sau — tất cả services đều phải có interface trong `IServices.cs`
- Dùng `ILogger<T>` injection, không dùng static logger
- Xử lý exception ở controller layer, không để exception bay ra ngoài
- Dùng `async/await` đúng cách — không `.Result` hay `.Wait()`

### Về Messenger
- `senderId` là session key cho Messenger conversations
- Debounce 5 giây đang là cơ chế merge message — không xóa
- Duplicate detection dùng 2 lớp: messageId cache + text fingerprint

---

## Định nghĩa "production ready"

- [ ] Không tạo duplicate booking sau confirm
- [ ] `cảm ơn`, `ok`, `dạ` sau booking không trigger booking flow
- [ ] Low-value messages không tốn AI call
- [ ] Messenger page map đúng hotel
- [ ] Google Sheets không bị rate limit khi nhiều user
- [ ] Có log đủ để debug incident booking
- [ ] Booking ID không trùng
- [ ] Cost AI đo được theo hotel

---

## Tài liệu tham khảo

- `docs/PRD.md` — Product requirements đầy đủ
- `docs/HOTEL_CHATBOT_PRODUCTION_BLUEPRINT.md` — Kiến trúc mục tiêu
- `docs/IMPLEMENTATION_ROADMAP.md` — 8 phases implementation
- `docs/RELEASE_GAP_ANALYSIS.md` — So sánh hiện trạng vs mong muốn
- `docs/RAG_QDRANT_DESIGN.md` — Thiết kế RAG/Qdrant chi tiết
- `docs/reports/CODE_REVIEW_2026_03_31.md` — Code review report
