# RAG Qdrant Design

## Goal

Tích hợp Retrieval-Augmented Generation (RAG) cho `ChatHotelAIBackend` theo hướng:
- không phá flow hiện tại
- giữ Google Sheets làm source of truth cho dữ liệu nghiệp vụ
- dùng Qdrant cho semantic retrieval
- bật/tắt được bằng config
- dễ scale sang nhiều khách sạn

## Current State

Hiện hệ thống:
- đọc dữ liệu từ Google Sheets
- tổng hợp toàn bộ context bằng `BuildHotelContextAsync`
- đẩy cả khối context đó vào prompt AI

Điểm yếu của cách này:
- prompt dài
- tốn token
- khó scale khi nhiều FAQ, phòng, tiện ích, khuyến mãi
- retrieval không theo đúng intent/user query

## Design Principles

1. Google Sheets vẫn là nguồn dữ liệu chuẩn cho:
- availability
- pricing runtime
- bookings
- promotion applicability logic

2. Qdrant chỉ là knowledge layer cho:
- FAQ
- hotel info
- room descriptions
- policies
- amenities
- promotions mang tính mô tả

3. Booking/availability động không lấy từ vector DB để ra quyết định cuối cùng.

4. RAG được đưa vào như tầng augment trước khi gọi AI provider.

## Collections

Thiết kế đề xuất:
- 1 collection duy nhất: `hotel_knowledge_v1`

Lý do:
- cùng 1 embedding space
- filter theo `hotel_id`
- dễ scale multi-hotel
- tránh tạo quá nhiều collection nhỏ

## Vector Strategy

Phase 1:
- dense vector only
- cosine similarity

Embedding model đề xuất:
- `text-embedding-3-small`

Phase 2:
- named vectors hoặc hybrid dense+sparse
- rerank nếu cần

## Payload Schema

Mỗi point trong Qdrant là một knowledge chunk.

Payload chuẩn:

```json
{
  "tenant": "prod",
  "hotel_id": "intercontinental_nhatrang",
  "doc_type": "faq",
  "entity_type": "FAQ",
  "entity_id": "faq_001",
  "chunk_id": "faq_001#0",
  "language": "vi",
  "title": "Check-in lúc mấy giờ?",
  "text": "Giờ check-in tiêu chuẩn là 15:00 và check-out là 12:00.",
  "room_type": null,
  "category": "CheckIn",
  "tags": ["policy", "frontdesk"],
  "keywords": ["checkin", "checkout"],
  "priority": 1,
  "is_active": true,
  "source_sheet": "FAQs",
  "source_row_key": "faq_001",
  "updated_at": "2026-03-29T00:00:00Z",
  "version": 1
}
```

## Indexed Payload Fields

Các field nên dùng filter/index:
- `hotel_id`
- `doc_type`
- `entity_type`
- `entity_id`
- `room_type`
- `category`
- `language`
- `is_active`
- `tags`

## Knowledge Document Types

Các loại documents ingest vào vector DB:
- `hotel_info_overview`
- `hotel_info_location`
- `hotel_info_policies`
- `room`
- `amenity`
- `promotion`
- `faq`

## Chunking Rules

### HotelInfo

Tách thành các chunk nhỏ:
- overview
- location
- policies
- check-in/check-out
- payment/pet/child/smoking

### Rooms

1 room record = 1 chunk:
- room type
- room type name
- size
- occupancy
- bed
- view
- description
- key amenities

### Amenities

1 amenity = 1 chunk

Có thể bổ sung chunk tổng hợp theo category ở phase sau.

### Promotions

1 promotion = 1 chunk

Bao gồm:
- tiêu đề
- mô tả
- điều kiện
- ngày hiệu lực
- room types áp dụng

### FAQs

1 FAQ = 1 chunk

Đây là nguồn có tỉ lệ retrieval cao nhất cho chatbot.

## Chunk ID Strategy

ID point phải deterministic để reindex/upsert dễ dàng:

Format:
- `{hotel_id}:{doc_type}:{entity_id}:{chunk_no}`

Ví dụ:
- `intercontinental_nhatrang:faq:faq_001:0`
- `intercontinental_nhatrang:room:ICNH-0801:0`
- `intercontinental_nhatrang:hotel_info:policies:0`

## Search Strategy

Khi user chat:
1. phân tích intent
2. search Qdrant với query là user message
3. filter theo `hotel_id`
4. ưu tiên theo intent
5. lấy top `k = 5`
6. ghép vào prompt dưới section riêng

## Intent-Aware Retrieval

Map intent sang doc types:

- `policy_inquiry`
  - `faq`, `hotel_info_overview`, `hotel_info_policies`

- `amenity_inquiry`
  - `amenity`, `room`

- `promotion_inquiry`
  - `promotion`, `faq`

- `location_inquiry`
  - `hotel_info_location`, `faq`

- `price_inquiry`
  - `room`, `promotion`
  - giá cuối cùng vẫn lấy từ logic structured

- `availability_check`
  - `room`
  - availability thật vẫn lấy từ sheets

- `booking_confirm`
  - ưu tiên structured flow, không phụ thuộc vector DB để chốt giao dịch

## Runtime Prompt Composition

Prompt cuối cùng nên gồm:

1. System prompt nền
2. structured runtime context
   - availability
   - pricing
   - booking state
3. retrieved knowledge context

Section gợi ý:

```text
=== TRI THỨC LIÊN QUAN TỪ KNOWLEDGE BASE ===
- ...
- ...
```

## Configuration

Thêm các section:

```json
"RAG": {
  "Enabled": false,
  "TopK": 5,
  "ScoreThreshold": 0.65,
  "UseIntentFilter": true,
  "UseRagForChat": true,
  "ReplaceFullHotelContext": false
}
```

```json
"Qdrant": {
  "BaseUrl": "http://localhost:6333",
  "ApiKey": "",
  "CollectionName": "hotel_knowledge_v1",
  "VectorSize": 1536
}
```

```json
"OpenAIEmbedding": {
  "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
  "Model": "text-embedding-3-small"
}
```

## Service Design

### Core Interfaces

- `IRagContextService`
  - build retrieval context cho chat runtime

- `IKnowledgeIndexingService`
  - build documents từ Google Sheets
  - embed
  - upsert vào Qdrant

- `IEmbeddingService`
  - gọi embedding provider

- `IVectorStoreService`
  - create collection
  - upsert points
  - search points
  - delete by hotel

### Main Infrastructure Classes

- `RagOptions`
- `QdrantOptions`
- `OpenAIEmbeddingOptions`
- `KnowledgeDocument`
- `KnowledgeChunk`
- `KnowledgeSearchRequest`
- `KnowledgeSearchResult`
- `HotelKnowledgeDocumentFactory`
- `OpenAIEmbeddingService`
- `QdrantVectorStoreService`
- `RagContextService`
- `KnowledgeIndexingService`
- `NoOpRagContextService`

## Suggested File Layout

```text
src/HotelChatbot.Core/
  IServices.cs

src/HotelChatbot.Infrastructure/RAG/
  RagOptions.cs
  QdrantOptions.cs
  OpenAIEmbeddingOptions.cs
  Models/
    KnowledgeDocument.cs
    KnowledgeChunk.cs
    KnowledgeSearchRequest.cs
    KnowledgeSearchResult.cs
  Services/
    HotelKnowledgeDocumentFactory.cs
    OpenAIEmbeddingService.cs
    QdrantVectorStoreService.cs
    RagContextService.cs
    KnowledgeIndexingService.cs
    NoOpRagContextService.cs
```

## API / Admin Endpoints

Nên thêm admin endpoints:
- `POST /api/rag/reindex/{hotelId}`
- `POST /api/rag/search`

Mục tiêu:
- reindex theo hotel
- preview retrieval

## Initial Integration Scope

Phase này chỉ làm:
- scaffold service/interfaces/options/models
- hook retrieval vào chat flow nếu `RAG:Enabled=true`
- admin endpoint để reindex và search preview

Chưa làm ở phase đầu:
- hybrid dense+sparse
- reranking
- streaming ingest
- background scheduler

## Safe Rollout Strategy

1. merge code với `RAG:Enabled=false`
2. deploy
3. cấu hình Qdrant + embeddings
4. gọi reindex cho từng hotel
5. bật `RAG:Enabled=true` sau khi kiểm tra

## Success Criteria

- build clean
- startup clean khi `RAG:Enabled=false`
- retrieval chỉ hoạt động khi config bật
- chat flow hiện tại không bị gãy
- structured booking logic vẫn authoritative
