# GPT Cost Analysis Report

Updated: 2026-03-29
Project: ChatHotelAIBackend
Scope: OpenAI GPT-only cost analysis for production operations and pricing design

## 1. Executive Summary

Current code structure makes each user message more expensive than a typical single-call chatbot because one chat turn usually triggers:

1. One GPT call for intent analysis
2. One GPT call for the final customer-facing answer
3. One embeddings call when RAG is enabled

Based on the current codebase and configuration, a realistic working estimate for `gpt-4.1-mini` is:

- Average cost per user turn: **~$0.0024**
- Average cost per conversation (5 turns): **~$0.0121**
- 1,000 conversations/month: **~$12.1 AI cost**
- 3,000 conversations/month: **~$36.2 AI cost**
- 10,000 conversations/month: **~$120.7 AI cost**

After adding a 20% operational buffer for retries, occasional long prompts, and noisy user behavior:

- 1,000 conversations/month: **~$14.5**
- 3,000 conversations/month: **~$43.4**
- 10,000 conversations/month: **~$144.8**

Important finding: the current RAG integration is **not yet delivering maximum cost savings**, because the app still builds the full hotel context on every main chat call while also adding retrieved RAG chunks.

## 2. What The Current Code Actually Does

### 2.1 Per user message call flow

From the current code:

- `ConversationService.ProcessMessageAsync(...)`
- `HotelAIServiceBase.AnalyzeIntentAsync(...)`
- `OpenAIService.GetChatCompletionAsync(...)`
- `RagContextService.BuildKnowledgeContextAsync(...)`
- `HotelDataService.BuildHotelContextAsync(...)`

Each user message usually triggers this sequence:

1. `AnalyzeIntentAsync(...)`
   - GPT call #1
   - Purpose: classify intent and extract entities

2. `BuildSystemPromptAsync(...)`
   - No API call by itself
   - But it loads and concatenates a large hotel context string

3. `BuildKnowledgeContextAsync(...)`
   - Embeddings call when RAG is enabled
   - Qdrant search
   - Retrieved chunks appended into prompt

4. `GetChatCompletionAsync(...)`
   - GPT call #2
   - Generates the final answer returned to the client

### 2.2 Current token-heavy behaviors

The biggest cost drivers in the current implementation are:

- Full hotel context is rebuilt and appended for the main answer call
- Session history is retained up to 20 messages
- Intent analysis uses a full GPT call instead of a lightweight rules engine or cheaper dedicated classifier
- RAG is enabled, but `ReplaceFullHotelContext` is not actually applied in the current runtime path

This means the current architecture is closer to:

`full prompt + history + RAG chunks`

instead of the cheaper target architecture:

`short system prompt + small structured runtime context + RAG chunks`

## 3. Current Configuration Relevant To Cost

From the current app config:

- Provider: `OpenAI`
- Main model: `gpt-4.1-mini`
- `MaxOutputTokens = 1024`
- RAG: enabled
- Embeddings model: `text-embedding-3-small`
- Max history retained in memory: `20 messages`

Important limit notes:

- The code caps output per GPT call at `1024`
- The app makes **2 GPT chat calls per user turn**
- So the theoretical output cap per turn is roughly `2048 output tokens`
- The code does **not** currently enforce a hard input token limit at app level

## 4. Official Pricing Used For This Analysis

This report uses official OpenAI pricing references:

- [OpenAI API Pricing](https://platform.openai.com/docs/pricing/)
- [GPT-4.1 mini model page](https://platform.openai.com/docs/models/gpt-4.1-mini)
- [GPT-4o mini model page](https://platform.openai.com/docs/models/gpt-4o-mini)
- [text-embedding-3-small model page](https://platform.openai.com/docs/models/text-embedding-3-small)
- [Chat Completions usage fields](https://platform.openai.com/docs/api-reference/chat-streaming)

Pricing assumptions used in the calculations below:

### 4.1 `gpt-4.1-mini`

- Input: **$0.40 / 1M tokens**
- Output: **$1.60 / 1M tokens**

### 4.2 `gpt-4o-mini`

- Input: **$0.15 / 1M tokens**
- Output: **$0.60 / 1M tokens**

### 4.3 `text-embedding-3-small`

- Embeddings: **$0.02 / 1M tokens**

## 5. Token Estimation Model

The codebase does not yet persist actual token usage from the OpenAI `usage` object, so the numbers here are **operational estimates**, not billing-ledger numbers.

### 5.1 Formula per user turn

For the current code path:

`Turn Cost = Intent GPT Cost + Main GPT Cost + Query Embedding Cost`

Expanded:

`Turn Cost = ((IntentInput + MainInput) * ChatInputRate + (IntentOutput + MainOutput) * ChatOutputRate + EmbeddingInput * EmbeddingRate) / 1,000,000`

### 5.2 Assumptions for current production-like behavior

These are based on the reviewed code, the existing seed data, and the current prompt-building strategy:

- Intent input is small to medium
- Main answer input is large because the hotel context is rebuilt every turn
- Output is usually moderate
- Embedding query is small
- Multi-turn sessions gradually increase main input because history is appended

## 6. Estimated Cost Per Turn With `gpt-4.1-mini`

### 6.1 Scenario A: Simple FAQ turn

Example:
- check-in time
- breakfast included
- pool hours

Estimated tokens:

- Intent input: 250
- Intent output: 120
- Main input: 3,200
- Main output: 350
- Embedding input: 35

Estimated cost:

- GPT input cost: `(250 + 3200) * 0.40 / 1,000,000 = $0.00138`
- GPT output cost: `(120 + 350) * 1.60 / 1,000,000 = $0.000752`
- Embedding cost: `35 * 0.02 / 1,000,000 = $0.0000007`
- Total: **~$0.00213 / turn**

### 6.2 Scenario B: Price or availability turn

Example:
- check dates
- show room options
- compare rates

Estimated tokens:

- Intent input: 300
- Intent output: 140
- Main input: 4,200
- Main output: 550
- Embedding input: 50

Estimated cost:

- GPT input cost: `(300 + 4200) * 0.40 / 1,000,000 = $0.00180`
- GPT output cost: `(140 + 550) * 1.60 / 1,000,000 = $0.001104`
- Embedding cost: `50 * 0.02 / 1,000,000 = $0.000001`
- Total: **~$0.00291 / turn**

### 6.3 Scenario C: Booking-heavy turn

Example:
- confirm guest info
- summarize booking
- produce final confirmation message

Estimated tokens:

- Intent input: 350
- Intent output: 160
- Main input: 5,600
- Main output: 800
- Embedding input: 60

Estimated cost:

- GPT input cost: `(350 + 5600) * 0.40 / 1,000,000 = $0.00238`
- GPT output cost: `(160 + 800) * 1.60 / 1,000,000 = $0.001536`
- Embedding cost: `60 * 0.02 / 1,000,000 = $0.0000012`
- Total: **~$0.00392 / turn**

## 7. Weighted Average Cost Per Turn

A realistic hotel traffic mix for this product is approximately:

- 70% simple FAQ / policy / amenity questions
- 25% price / availability / room comparison
- 5% booking-heavy turns

Weighted average turn cost:

- `0.70 * 0.00213 = 0.001491`
- `0.25 * 0.00291 = 0.0007275`
- `0.05 * 0.00392 = 0.000196`

Estimated weighted average:

- **~$0.00241 / turn**

Rounded operating estimate:

- **~$0.0024 per user turn**

## 8. Estimated Cost Per Conversation

Assume a normal hotel conversation has:

- Average turns per conversation: `5`

Then:

- `5 * $0.00241 = $0.01205`

Rounded:

- **~$0.0121 per conversation**

For longer booking-oriented conversations:

- 6 to 8 turns
- expected cost range: **~$0.0145 to $0.0193 per conversation**

## 9. Monthly AI Cost Forecast Per Hotel

### 9.1 Base AI cost only

| Monthly conversations | Avg turns | Estimated AI cost |
| --- | ---: | ---: |
| 1,000 | 5 | ~$12.1 |
| 3,000 | 5 | ~$36.2 |
| 5,000 | 5 | ~$60.3 |
| 10,000 | 5 | ~$120.7 |
| 20,000 | 5 | ~$241.4 |

### 9.2 With 20% operational buffer

This buffer covers:

- retries
- malformed user input
- long-tail sessions
- prompt growth from conversation history
- occasional fallback behavior

| Monthly conversations | Estimated AI cost with 20% buffer |
| --- | ---: |
| 1,000 | ~$14.5 |
| 3,000 | ~$43.4 |
| 5,000 | ~$72.4 |
| 10,000 | ~$144.8 |
| 20,000 | ~$289.7 |

## 10. Worst-Case Guardrail Estimate

The code currently allows a fairly large prompt and up to `1024` output tokens per GPT call.

A conservative worst-case turn estimate for current architecture is:

- Combined GPT input: `15,000 tokens`
- Combined GPT output: `2,048 tokens`
- Embedding input: ignored because it is tiny compared with chat cost

Cost with `gpt-4.1-mini`:

- Input: `15,000 * 0.40 / 1,000,000 = $0.0060`
- Output: `2,048 * 1.60 / 1,000,000 = $0.00328`
- Total: **~$0.0093 / turn**

For a 6-turn long conversation, this would be:

- **~$0.0558 / conversation**

This is not the expected average, but it is a useful safety ceiling for pricing and margin planning.

## 11. RAG Cost Impact

### 11.1 Query-time embedding cost

Query embeddings are cheap.

Even at 100 embedding tokens per turn:

- `100 * 0.02 / 1,000,000 = $0.000002`

This is negligible compared with GPT chat cost.

### 11.2 Indexing cost

Reindexing hotel knowledge into Qdrant is also cheap relative to chat usage.

In practice:

- index cost is occasional
- chat cost is continuous
- GPT chat is the main cost driver, not embeddings

### 11.3 Current limitation

The current codebase still rebuilds the full hotel context for each main GPT answer, then appends RAG context on top.

So today, RAG helps answer relevance, but it does **not yet minimize token cost as much as it could**.

## 12. Total Operating Cost Beyond AI

If you want pricing that works in production, you should not price off raw OpenAI cost alone.

Typical non-AI operating costs to account for:

- API hosting for ASP.NET service
- Qdrant hosting or VM resources
- logs and monitoring
- backup / secret management
- engineering support and incident handling
- customer onboarding and data setup

A practical early-stage fixed monthly platform cost range is:

- **~$45 to $90 / month total platform fixed cost**

If this fixed cost is shared across multiple hotel customers:

| Number of hotels | Fixed-cost share per hotel |
| --- | ---: |
| 5 hotels | ~$9 to $18 |
| 10 hotels | ~$4.5 to $9 |
| 20 hotels | ~$2.25 to $4.5 |

## 13. Recommended Pricing Strategy

Do **not** sell this product as a pure token resale service.

Hotels buy:

- 24/7 lead capture
- faster response time
- fewer missed bookings
- automated FAQ and booking support
- multilingual concierge experience

So pricing should be based on **business value + conversation volume**, not only AI cost.

### 13.1 Recommended launch principles

1. Use included monthly conversation quotas
2. Add overage pricing after quota
3. Keep margins high enough to survive noisy usage and support effort
4. Separate setup / onboarding from monthly subscription
5. Avoid unlimited plans at launch

## 14. Recommended Real-World Pricing Plans

These suggestions assume the current stack still uses `gpt-4.1-mini`.

Exchange-rate note for internal planning:

- Use a rough internal planning rate of **1 USD = 26,000 VND**

### 14.1 Starter

Target:

- boutique hotel
- low to moderate inquiry volume
- 10 to 40 rooms

Included:

- 1 hotel property
- up to `1,500 conversations / month`
- basic analytics
- standard support

Estimated AI variable cost:

- `1,500 * $0.0121 = ~$18.1`
- with buffer: **~$21.7**

Suggested sell price:

- **2,490,000 VND / month**

Rationale:

- healthy margin
- affordable for small hotels
- covers onboarding and support much better than token pass-through pricing

### 14.2 Growth

Target:

- city hotel or resort with active digital inquiries
- stronger booking and pricing traffic

Included:

- 1 hotel property
- up to `5,000 conversations / month`
- better analytics
- RAG knowledge base maintenance
- priority support

Estimated AI variable cost:

- `5,000 * $0.0121 = ~$60.3`
- with buffer: **~$72.4**

Suggested sell price:

- **5,900,000 VND / month**

Rationale:

- still easy for hotels to justify if the bot saves just a few manual leads per month
- creates room for support and product improvement costs

### 14.3 Pro

Target:

- resort group
- large hotel
- high inbound traffic
- marketing campaigns and seasonal spikes

Included:

- 1 hotel property
- up to `15,000 conversations / month`
- advanced analytics
- priority SLA
- richer integrations / custom workflows

Estimated AI variable cost:

- `15,000 * $0.0121 = ~$181.0`
- with buffer: **~$217.2**

Suggested sell price:

- **11,900,000 VND / month**

Rationale:

- enough room for enterprise handling and higher support burden
- still attractive compared with staffing cost or lost bookings

### 14.4 Overage pricing

Recommended overage:

- **1,500 to 2,500 VND / extra conversation**

Why this is reasonable:

- estimated AI cost per conversation is only around `315 VND`
- overage must also cover support, unpredictable long sessions, and margin
- simple per-conversation overage is easier for hotels to understand than token billing

## 15. Alternative Cost-Down Option Inside OpenAI

If later you switch the main chat model from `gpt-4.1-mini` to `gpt-4o-mini`, AI variable cost can drop materially.

Rough relative effect:

- input cost drops by about 62.5%
- output cost drops by about 62.5%

That gives you two business options:

1. Keep current sell price and expand gross margin
2. Keep margin similar and offer a lower-cost plan for smaller hotels

This is the cleanest future cost-down lever if answer quality remains acceptable in production.

## 16. What Should Be Implemented Next For Accurate Billing

Right now the app estimates are useful for planning, but not enough for precise internal profitability tracking.

The next must-have implementation is token telemetry.

### 16.1 Add usage tracking from OpenAI responses

For chat completions:

- parse `usage.prompt_tokens`
- parse `usage.completion_tokens`
- parse `usage.total_tokens`

For embeddings:

- parse embedding usage tokens if available
- log request size and cumulative indexing cost

### 16.2 Store usage by dimensions that matter

At minimum, persist or log:

- `timestamp`
- `sessionId`
- `hotelId`
- `provider`
- `model`
- `operation`
  - `intent_analysis`
  - `chat_reply`
  - `embedding_query`
  - `embedding_index`
- `input_tokens`
- `output_tokens`
- `estimated_cost_usd`

### 16.3 Build three dashboards

1. Cost per hotel per month
2. Cost per conversation
3. Booking conversion vs AI cost

That will let you answer the most important business question:

- which hotel is profitable
- which hotel needs a higher quota tier
- which workloads should move to a cheaper model

## 17. Highest-Impact Cost Optimizations For This Codebase

These are listed in priority order.

### 17.1 Make RAG replace most of the full hotel context

Highest impact.

Current problem:

- large hotel context is still appended every turn
- RAG currently adds retrieved chunks on top of that

Expected effect after fixing this:

- reduce main-input tokens significantly
- likely save **30% to 60%** on chat cost depending on hotel data size

### 17.2 Use a cheaper path for intent analysis

Current problem:

- every turn spends a separate GPT call on intent detection

Expected effect:

- save roughly **15% to 30%** of variable AI cost depending on traffic profile

Possible approach:

- simple rules for obvious intents
- or keep OpenAI but use a cheaper smaller model for classification only

### 17.3 Reduce history sent to the main answer call

Current problem:

- session keeps up to 20 messages
- long sessions gradually inflate input cost

Expected effect:

- lower long-tail prompt bloat
- better cost predictability

### 17.4 Separate structured truth from narrative context

Keep these outside large free-text prompt blocks whenever possible:

- availability
- booking state
- pricing totals
- promotions applied

This improves both correctness and token efficiency.

## 18. Final Recommendation

If you are launching soon and want a practical pricing model now:

- Keep `gpt-4.1-mini` for launch quality stability
- Sell by monthly hotel subscription with included conversation quotas
- Start with `Starter / Growth / Pro`
- Do not expose token billing to hotel customers
- Add overage billing per conversation, not per token
- Implement token telemetry immediately after release prep
- Prioritize replacing the full hotel context with RAG-driven context to lower cost before scale

Best current launch recommendation:

- `Starter`: **2,490,000 VND / month**
- `Growth`: **5,900,000 VND / month**
- `Pro`: **11,900,000 VND / month**
- Overage: **1,500 to 2,500 VND / conversation**

This gives you a pricing structure that is understandable for hotels, operationally safe, and still leaves room for optimization as real usage data comes in.

