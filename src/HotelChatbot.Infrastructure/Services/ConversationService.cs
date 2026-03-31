using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace HotelChatbot.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly IHotelAIService _ai;
    private readonly ITextPreprocessorService _preprocessor;
    private readonly IRagContextService _ragContext;
    private readonly IHotelDataService _hotelData;
    private readonly IBookingService _bookingService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConversationService> _logger;

    private const int MAX_HISTORY = 20; // Keep last 20 messages
    private const int SESSION_TIMEOUT_MINUTES = 30;

    private static readonly string[] LowValuePatterns =
        ["ok", "oke", "okay", "dạ", "vâng", "ừ", "cảm ơn", "cam on", "thanks", "thank", "hello", "hi", "chào", "chao", "👍", "🙏"];

    private static bool IsLowValueMessage(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized.Length < 20
            && LowValuePatterns.Any(p => normalized == p || normalized.Contains(p));
    }

    public ConversationService(
        IHotelAIService ai,
        ITextPreprocessorService preprocessor,
        IRagContextService ragContext,
        IHotelDataService hotelData,
        IBookingService bookingService,
        IMemoryCache cache,
        ILogger<ConversationService> logger)
    {
        _ai = ai;
        _preprocessor = preprocessor;
        _ragContext = ragContext;
        _hotelData = hotelData;
        _bookingService = bookingService;
        _cache = cache;
        _logger = logger;
    }

    public ConversationSession GetOrCreateSession(string sessionId, string hotelId)
    {
        var cacheKey = $"session_{sessionId}";
        if (_cache.TryGetValue(cacheKey, out ConversationSession? session) && session != null)
        {
            session.LastActivityAt = DateTime.UtcNow;
            return session;
        }

        session = new ConversationSession
        {
            SessionId = sessionId,
            HotelId = hotelId,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, session, TimeSpan.FromMinutes(SESSION_TIMEOUT_MINUTES));
        return session;
    }

    public void UpdateSession(ConversationSession session)
    {
        var cacheKey = $"session_{session.SessionId}";
        session.LastActivityAt = DateTime.UtcNow;
        _cache.Set(cacheKey, session, TimeSpan.FromMinutes(SESSION_TIMEOUT_MINUTES));
    }

    public void AddMessageToSession(string sessionId, string role, string content)
    {
        var cacheKey = $"session_{sessionId}";
        if (_cache.TryGetValue(cacheKey, out ConversationSession? session) && session != null)
        {
            session.Messages.Add(new ConversationMessage { Role = role, Content = content });

            // Keep only recent messages to avoid token overflow
            if (session.Messages.Count > MAX_HISTORY)
                session.Messages = session.Messages.TakeLast(MAX_HISTORY).ToList();

            UpdateSession(session);
        }
    }

    // ============================================================
    // MAIN: PROCESS MESSAGE
    // ============================================================
    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request)
    {
        var sessionId = string.IsNullOrEmpty(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId;

        var session = GetOrCreateSession(sessionId, request.HotelId);

        // Cheap-path: skip cả 2 AI calls cho low-value messages sau khi booking đã hoàn tất
        // Tránh AI classify sai "cảm ơn", "ok" thành booking_confirm và tạo booking thứ 2
        if (session.BookingStatus == BookingSessionStatus.Completed && IsLowValueMessage(request.Message))
        {
            _logger.LogInformation(
                "[CheapPath] Session {SessionId} post-booking low-value message, skipping AI calls",
                sessionId);

            const string templateReply = "Dạ, em rất vui được hỗ trợ anh/chị! 😊 Nếu cần thêm gì, anh/chị cứ nhắn em nhé.";
            session.Messages.Add(new ConversationMessage { Role = "user", Content = request.Message });
            session.Messages.Add(new ConversationMessage { Role = "assistant", Content = templateReply });
            UpdateSession(session);

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = templateReply,
                Intent = "general",
                IsBookingComplete = true,
                BookingId = session.LastBookingId
            };
        }

        // [PARALLEL] Preprocessing + system prompt chạy đồng thời để tiết kiệm latency
        // Preprocessing dùng gpt-4o-mini: phục hồi dấu tiếng Việt, tạo RAG query tối ưu, detect language
        var preprocessTask = _preprocessor.PreprocessAsync(request.Message, request.HotelId, sessionId);
        var systemPromptTask = _ai.BuildSystemPromptAsync(request.HotelId);
        await Task.WhenAll(preprocessTask, systemPromptTask);

        var preprocessed = preprocessTask.Result;
        var systemPrompt = systemPromptTask.Result;

        // Dùng normalized text cho intent analysis — tốt hơn khi user nhắn thiếu dấu
        var messageForIntent = preprocessed.Skipped ? request.Message : preprocessed.Normalized;

        // Analyze intent
        var intent = await _ai.AnalyzeIntentAsync(messageForIntent, session);
        ApplyDeterministicRelativeDateOverrides(messageForIntent, intent.ExtractedEntities);

        // Update booking draft with extracted entities
        UpdateBookingDraftFromEntities(session, intent.ExtractedEntities);

        // Dùng RAG query tối ưu từ preprocessor — chính xác hơn raw message
        var ragQuery = preprocessed.Skipped ? request.Message : preprocessed.RagQuery;
        var ragKnowledgeContext = await _ragContext.BuildKnowledgeContextAsync(
            request.HotelId,
            ragQuery,
            intent.Intent,
            sessionId);
        if (!string.IsNullOrWhiteSpace(ragKnowledgeContext))
            systemPrompt += $"\n\n=== TRI THỨC LIÊN QUAN TỪ KNOWLEDGE BASE ===\n{ragKnowledgeContext}";

        // Add availability context if needed
        if (intent.Intent is "availability_check" or "booking_init" or "price_inquiry")
        {
            var availabilityContext = await BuildAvailabilityContextAsync(session, intent);
            if (!string.IsNullOrEmpty(availabilityContext))
                systemPrompt += $"\n\n=== THÔNG TIN PHÒNG TRỐNG HIỆN TẠI ===\n{availabilityContext}";
        }

        // Inject current booking draft as ground truth so AI doesn't hallucinate dates/prices
        // This prevents the AI from summarizing wrong dates from conversation memory
        var bookingDraftContext = BuildBookingDraftContext(session.BookingDraft);
        if (!string.IsNullOrEmpty(bookingDraftContext))
            systemPrompt += $"\n\n=== THÔNG TIN ĐẶT PHÒNG HIỆN TẠI (GROUND TRUTH) ===\n{bookingDraftContext}\nKhi tóm tắt thông tin đặt phòng cho khách, PHẢI dùng đúng các giá trị trên. Không được suy đoán hoặc dùng con số khác.";

        // Get AI response
        var aiResponse = await _ai.GetChatCompletionAsync(
            systemPrompt,
            session.Messages,
            request.Message,
            "chat_reply",
            request.HotelId,
            sessionId);

        // Check if booking is ready to be created
        var isBookingReady = aiResponse.Contains("[BOOKING_READY]");
        var cleanResponse = aiResponse.Replace("[BOOKING_READY]", "").Trim();

        BookingConfirmationDto? confirmation = null;
        string? bookingId = null;
        var shouldAttemptBooking = isBookingReady || intent.Intent == "booking_confirm";

        // Hậu-booking guard: nếu session đã hoàn tất booking, không cho phép tạo thêm
        if (shouldAttemptBooking && session.BookingStatus == BookingSessionStatus.Completed)
        {
            _logger.LogInformation(
                "[BookingGuard] Session {SessionId} already completed booking {BookingId}, skipping booking flow",
                sessionId, session.LastBookingId);
            shouldAttemptBooking = false;
        }

        if (shouldAttemptBooking)
        {
            await TryResolveRoomIdAsync(session, request.HotelId);

            if (IsBookingDraftComplete(session.BookingDraft))
            {
                try
                {
                    var bookingRequest = BuildBookingRequest(session, request.HotelId);
                    confirmation = await _bookingService.CreateBookingAsync(bookingRequest);
                    bookingId = confirmation.BookingId;
                    cleanResponse = confirmation.ConfirmationMessage;
                    isBookingReady = true;

                    // Đánh dấu session đã hoàn tất booking — ngăn tạo lại khi user nhắn "cảm ơn", "ok", v.v.
                    session.BookingStatus = BookingSessionStatus.Completed;
                    session.LastBookingId = bookingId;
                    session.BookingDraft = new BookingDraftDto();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating booking for session {SessionId}", sessionId);
                    cleanResponse = $"Dạ, em xin lỗi, có lỗi xảy ra khi đặt phòng: {ex.Message}. Anh/chị vui lòng thử lại hoặc liên hệ trực tiếp với khách sạn nhé! 🙏";
                    isBookingReady = false;
                }
            }
            else if (intent.Intent == "booking_confirm")
            {
                cleanResponse = BuildBookingPendingMessage(session.BookingDraft);
                isBookingReady = false;
            }
        }

        // Save messages to session
        session.Messages.Add(new ConversationMessage { Role = "user", Content = request.Message });
        session.Messages.Add(new ConversationMessage { Role = "assistant", Content = cleanResponse });
        session.CurrentIntent = intent.Intent;
        UpdateSession(session);

        // Build room suggestions if applicable
        List<RoomSuggestionDto>? suggestions = null;
        if (intent.Intent is "availability_check" or "price_inquiry" or "booking_init")
        {
            suggestions = await BuildRoomSuggestionsAsync(session, intent, request.HotelId);
        }

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = cleanResponse,
            Intent = intent.Intent,
            BookingDraft = session.BookingDraft,
            RoomSuggestions = suggestions,
            IsBookingComplete = isBookingReady && bookingId != null,
            BookingId = bookingId,
            Timestamp = DateTime.UtcNow
        };
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private void UpdateBookingDraftFromEntities(ConversationSession session, Dictionary<string, string> entities)
    {
        var draft = session.BookingDraft;

        if (entities.TryGetValue("check_in_date", out var checkIn) && DateTime.TryParse(checkIn, out var checkInDate))
            draft.CheckInDate = checkInDate;

        if (entities.TryGetValue("check_out_date", out var checkOut) && DateTime.TryParse(checkOut, out var checkOutDate))
            draft.CheckOutDate = checkOutDate;

        if (entities.TryGetValue("num_adults", out var adults) && int.TryParse(adults, out var numAdults))
            draft.NumAdults = numAdults;

        if (entities.TryGetValue("num_children", out var children) && int.TryParse(children, out var numChildren))
            draft.NumChildren = numChildren;

        if (entities.TryGetValue("room_type", out var roomType) && !string.IsNullOrEmpty(roomType))
            draft.RoomType = roomType;

        if (entities.TryGetValue("guest_name", out var name) && !string.IsNullOrEmpty(name))
            draft.GuestName = name;

        if (entities.TryGetValue("guest_phone", out var phone) && !string.IsNullOrEmpty(phone))
            draft.GuestPhone = phone;

        if (entities.TryGetValue("promo_code", out var promo) && !string.IsNullOrEmpty(promo))
            draft.PromoCode = promo;
    }

    private void ApplyDeterministicRelativeDateOverrides(string message, Dictionary<string, string> entities)
    {
        var today = GetVietnamToday();
        var norm = message.Trim().ToLowerInvariant();

        // Priority 1: range "từ A đến B" → set both dates
        if (TryParseRangeExpression(norm, today, out var rangeIn, out var rangeOut))
        {
            entities["check_in_date"] = rangeIn!.Value.ToString("yyyy-MM-dd");
            entities["check_out_date"] = rangeOut!.Value.ToString("yyyy-MM-dd");
            return;
        }

        // Priority 2: duration "X đêm từ [date]" → check_in + check_out
        if (TryParseDurationExpression(norm, today, out var durIn, out var durOut))
        {
            entities["check_in_date"] = durIn!.Value.ToString("yyyy-MM-dd");
            entities["check_out_date"] = durOut!.Value.ToString("yyyy-MM-dd");
            return;
        }

        // Priority 3: standalone date expressions in order of appearance
        var dates = ExtractStandaloneDates(norm, today);
        if (dates.Count >= 1) entities["check_in_date"] = dates[0].ToString("yyyy-MM-dd");
        if (dates.Count >= 2) entities["check_out_date"] = dates[1].ToString("yyyy-MM-dd");
    }

    private static bool TryParseRangeExpression(string norm, DateTime today, out DateTime? checkIn, out DateTime? checkOut)
    {
        checkIn = null; checkOut = null;

        var m = Regex.Match(norm, @"từ\s+(.+?)\s+đến\s+(.+?)(?:\s*[,\.!?]|\s+(?:nhé|ạ|nha|thì|để|nhờ)|$)");
        if (!m.Success) return false;

        var d1 = TryParseSingleDatePhrase(m.Groups[1].Value.Trim(), today);
        var d2 = TryParseSingleDatePhrase(m.Groups[2].Value.Trim(), today);

        if (!d1.HasValue || !d2.HasValue || d2.Value <= d1.Value) return false;

        checkIn = d1; checkOut = d2;
        return true;
    }

    private static bool TryParseDurationExpression(string norm, DateTime today, out DateTime? checkIn, out DateTime? checkOut)
    {
        checkIn = null; checkOut = null;

        // "X đêm từ [date]" or "X đêm kể từ [date]"
        var m1 = Regex.Match(norm, @"(\d+)\s*đêm\s+(?:từ|kể từ|bắt đầu từ)\s+(.+?)(?:\s*[,\.!?]|\s+(?:nhé|ạ|nha)|$)");
        if (m1.Success && int.TryParse(m1.Groups[1].Value, out var n1) && n1 > 0)
        {
            var d = TryParseSingleDatePhrase(m1.Groups[2].Value.Trim(), today);
            if (d.HasValue) { checkIn = d; checkOut = d.Value.AddDays(n1); return true; }
        }

        // "ở/nghỉ [date] X đêm" or "ở [date] trong X đêm"
        var m2 = Regex.Match(norm, @"(?:ở|nghỉ)\s+(.+?)\s+(?:trong\s+)?(\d+)\s*đêm");
        if (m2.Success && int.TryParse(m2.Groups[2].Value, out var n2) && n2 > 0)
        {
            var d = TryParseSingleDatePhrase(m2.Groups[1].Value.Trim(), today);
            if (d.HasValue) { checkIn = d; checkOut = d.Value.AddDays(n2); return true; }
        }

        return false;
    }

    private static DateTime? TryParseSingleDatePhrase(string phrase, DateTime today)
    {
        phrase = phrase.Trim().ToLowerInvariant();

        switch (phrase)
        {
            case "hôm nay": case "today": return today;
            case "mai": case "ngày mai": return today.AddDays(1);
            case "mốt": case "ngày mốt": return today.AddDays(2);
            case "ngày kia": case "ngày kìa": return today.AddDays(3);
            case "cuối tuần": case "cuối tuần này": return GetWeekendDate(today, nextWeek: false);
            case "cuối tuần tới": case "cuối tuần sau": return GetWeekendDate(today, nextWeek: true);
        }

        // Weekday + tuần: "thứ X tuần tới/này/sau"
        var wdWeek = Regex.Match(phrase, @"^(thứ\s*[2-7]|chủ\s*nhật|cn)\s+tuần\s+(tới|sau|này)$");
        if (wdWeek.Success)
            return ResolveWeekdayRelativeDate(NormalizeWeekdayText(wdWeek.Groups[1].Value), wdWeek.Groups[2].Value, today);

        // Just weekday without qualifier → nearest future
        var wdOnly = Regex.Match(phrase, @"^(thứ\s*[2-7]|chủ\s*nhật|cn)$");
        if (wdOnly.Success)
            return ResolveWeekdayRelativeDate(NormalizeWeekdayText(wdOnly.Groups[1].Value), "tới", today);

        // dd/mm/yyyy or dd-mm-yyyy
        var fullDt = Regex.Match(phrase, @"^(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})$");
        if (fullDt.Success)
            return ParseAbsoluteDate(fullDt.Groups[1].Value, fullDt.Groups[2].Value, fullDt.Groups[3].Value, today);

        // dd/mm or dd-mm
        var shortDt = Regex.Match(phrase, @"^(\d{1,2})[/\-](\d{1,2})$");
        if (shortDt.Success)
            return ParseAbsoluteDate(shortDt.Groups[1].Value, shortDt.Groups[2].Value, null, today);

        // ngày DD tháng MM [năm YYYY] or DD tháng MM [năm YYYY]
        var namedDt = Regex.Match(phrase, @"^(?:ngày\s+)?(\d{1,2})\s+tháng\s+(\d{1,2})(?:\s+năm\s+(\d{4}))?$");
        if (namedDt.Success)
            return ParseAbsoluteDate(namedDt.Groups[1].Value, namedDt.Groups[2].Value,
                namedDt.Groups[3].Success ? namedDt.Groups[3].Value : null, today);

        // ngày X tháng tới/sau/này
        var relMonth = Regex.Match(phrase, @"^(?:ngày\s+)?(\d{1,2})\s+tháng\s+(tới|sau|này)$");
        if (relMonth.Success)
            return ResolveDayOfMonthRelativeDate(int.Parse(relMonth.Groups[1].Value), relMonth.Groups[2].Value, today);

        return null;
    }

    private static List<DateTime> ExtractStandaloneDates(string norm, DateTime today)
    {
        // (startIndex → date) — more specific patterns registered first win on same position
        var found = new SortedDictionary<int, DateTime>();

        void TryAdd(int pos, DateTime? date)
        {
            if (date.HasValue && date.Value.Date >= today && !found.ContainsKey(pos))
                found[pos] = date.Value.Date;
        }

        // 1. Absolute with year: dd/mm/yyyy or dd-mm-yyyy
        foreach (Match m in Regex.Matches(norm, @"\b(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})\b"))
            TryAdd(m.Index, ParseAbsoluteDate(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, today));

        // 2. ngày DD tháng MM [năm YYYY]
        foreach (Match m in Regex.Matches(norm, @"ngày\s+(\d{1,2})\s+tháng\s+(\d{1,2})(?:\s+năm\s+(\d{4}))?"))
            TryAdd(m.Index, ParseAbsoluteDate(m.Groups[1].Value, m.Groups[2].Value,
                m.Groups[3].Success ? m.Groups[3].Value : null, today));

        // 3. Simple keywords (longer phrases first to avoid "mai" inside "ngày mai")
        var simpleMap = new (string pattern, int days)[]
        {
            (@"\bhôm nay\b", 0), (@"\btoday\b", 0),
            (@"\bngày mai\b", 1), (@"\bmai\b", 1),
            (@"\bngày mốt\b", 2), (@"\bmốt\b", 2),
            (@"\bngày kia\b", 3), (@"\bngày kìa\b", 3),
        };
        foreach (var (pat, days) in simpleMap)
        {
            var km = Regex.Match(norm, pat);
            if (km.Success) TryAdd(km.Index, today.AddDays(days));
        }

        // 4. Cuối tuần tới trước cuối tuần này để tránh overlap
        var wkNext = Regex.Match(norm, @"cuối tuần\s+(tới|sau)");
        if (wkNext.Success) TryAdd(wkNext.Index, GetWeekendDate(today, nextWeek: true));

        var wkThis = Regex.Match(norm, @"cuối tuần(?!\s+(?:tới|sau))");
        if (wkThis.Success) TryAdd(wkThis.Index, GetWeekendDate(today, nextWeek: false));

        // 5. Weekday + tuần
        foreach (Match m in Regex.Matches(norm, @"(thứ\s*[2-7]|chủ\s*nhật|cn)\s+tuần\s+(tới|sau|này)"))
            TryAdd(m.Index, ResolveWeekdayRelativeDate(NormalizeWeekdayText(m.Groups[1].Value), m.Groups[2].Value, today));

        // 6. Ngày X tháng tới/sau/này
        foreach (Match m in Regex.Matches(norm, @"ngày\s+(\d{1,2})\s+tháng\s+(tới|sau|này)"))
            TryAdd(m.Index, ResolveDayOfMonthRelativeDate(int.Parse(m.Groups[1].Value), m.Groups[2].Value, today));

        // 7. DD tháng MM [năm YYYY] (without ngày prefix)
        foreach (Match m in Regex.Matches(norm, @"(?<![a-z])(\d{1,2})\s+tháng\s+(\d{1,2})(?:\s+năm\s+(\d{4}))?"))
        {
            if (!found.ContainsKey(m.Index))
                TryAdd(m.Index, ParseAbsoluteDate(m.Groups[1].Value, m.Groups[2].Value,
                    m.Groups[3].Success ? m.Groups[3].Value : null, today));
        }

        // 8. Absolute dd/mm (no year) — only where not already consumed by yyyy pattern
        foreach (Match m in Regex.Matches(norm, @"\b(\d{1,2})[/\-](\d{1,2})\b"))
        {
            if (!found.ContainsKey(m.Index))
                TryAdd(m.Index, ParseAbsoluteDate(m.Groups[1].Value, m.Groups[2].Value, null, today));
        }

        return found.Values.Distinct().Take(2).ToList();
    }

    private static DateTime? ParseAbsoluteDate(string dayStr, string monthStr, string? yearStr, DateTime today)
    {
        if (!int.TryParse(dayStr, out var day) || !int.TryParse(monthStr, out var month)) return null;

        int year;
        if (yearStr != null)
        {
            if (!int.TryParse(yearStr, out year)) return null;
        }
        else
        {
            year = today.Year;
        }

        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return null;

        var date = new DateTime(year, month, day);
        // Nếu không có năm và ngày đã qua → thử năm sau
        if (yearStr == null && date.Date < today)
            date = new DateTime(year + 1, month, day);

        return date.Date;
    }

    private static DateTime GetWeekendDate(DateTime today, bool nextWeek)
    {
        // Trả về Saturday gần nhất (≥ today), nextWeek = Saturday tuần sau đó
        var daysUntilSat = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var nearestSat = today.AddDays(daysUntilSat);
        return nextWeek ? nearestSat.AddDays(7) : nearestSat;
    }

    private static string NormalizeWeekdayText(string text)
    {
        // "thứ7" → "thứ 7", "thứ  2" → "thứ 2"
        return Regex.Replace(text.Trim(), @"thứ\s*([2-7])", "thứ $1");
    }

    private static DateTime? ResolveWeekdayRelativeDate(string weekdayText, string relativeText, DateTime today)
    {
        var dayOfWeek = weekdayText switch
        {
            "thứ 2" => DayOfWeek.Monday,
            "thứ 3" => DayOfWeek.Tuesday,
            "thứ 4" => DayOfWeek.Wednesday,
            "thứ 5" => DayOfWeek.Thursday,
            "thứ 6" => DayOfWeek.Friday,
            "thứ 7" => DayOfWeek.Saturday,
            "chủ nhật" => DayOfWeek.Sunday,
            "cn" => DayOfWeek.Sunday,
            _ => (DayOfWeek?)null
        };

        if (!dayOfWeek.HasValue) return null;

        var currentWeekMonday = today.AddDays(-((7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        var weekOffset = relativeText switch
        {
            "này" => 0,
            "tới" => 1,
            "sau" => 1,
            _ => 0
        };

        var targetWeekMonday = currentWeekMonday.AddDays(weekOffset * 7);
        var targetDate = targetWeekMonday.AddDays(((7 + (int)dayOfWeek.Value - (int)DayOfWeek.Monday) % 7));

        if (relativeText == "này" && targetDate.Date < today)
            targetDate = targetDate.AddDays(7);

        return targetDate.Date;
    }

    private static DateTime? ResolveDayOfMonthRelativeDate(int day, string relativeText, DateTime today)
    {
        if (day < 1 || day > 31) return null;

        var monthOffset = relativeText switch
        {
            "này" => 0,
            "tới" => 1,
            "sau" => 1,
            _ => 0
        };

        var targetMonth = new DateTime(today.Year, today.Month, 1).AddMonths(monthOffset);
        if (day > DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month))
            return null;

        var targetDate = new DateTime(targetMonth.Year, targetMonth.Month, day);
        if (relativeText == "này" && targetDate.Date < today)
            return targetDate.AddMonths(1);

        return targetDate.Date;
    }

    private static DateTime GetVietnamToday()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            }
            catch
            {
                return DateTime.UtcNow.AddHours(7).Date;
            }
        }
    }

    private async Task<string> BuildAvailabilityContextAsync(ConversationSession session, IntentAnalysis intent)
    {
        if (!session.BookingDraft.CheckInDate.HasValue || !session.BookingDraft.CheckOutDate.HasValue)
            return "";

        try
        {
            var availableRooms = await _hotelData.GetAvailableRoomsAsync(
                session.HotelId,
                session.BookingDraft.CheckInDate.Value,
                session.BookingDraft.CheckOutDate.Value,
                session.BookingDraft.NumAdults ?? 2,
                session.BookingDraft.NumChildren ?? 0);

            if (!availableRooms.Any())
                return "Hiện tại KHÔNG CÒN PHÒNG TRỐNG cho khoảng thời gian này. Hãy đề xuất khách chọn ngày khác.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Khoảng thời gian: {session.BookingDraft.CheckInDate:dd/MM/yyyy} - {session.BookingDraft.CheckOutDate:dd/MM/yyyy}");
            sb.AppendLine($"Số đêm: {(session.BookingDraft.CheckOutDate.Value - session.BookingDraft.CheckInDate.Value).Days}");
            sb.AppendLine("Phòng còn trống:");

            foreach (var room in availableRooms)
            {
                var price = await _hotelData.CalculatePriceAsync(
                    session.HotelId, room.RoomType,
                    session.BookingDraft.CheckInDate.Value,
                    session.BookingDraft.CheckOutDate.Value);

                sb.AppendLine($"- {room.RoomTypeName} (Phòng {room.RoomNumber}): {price:N0} VND tổng {(session.BookingDraft.CheckOutDate.Value - session.BookingDraft.CheckInDate.Value).Days} đêm");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building availability context");
            return "";
        }
    }

    private async Task<List<RoomSuggestionDto>?> BuildRoomSuggestionsAsync(
        ConversationSession session, IntentAnalysis intent, string hotelId)
    {
        if (!session.BookingDraft.CheckInDate.HasValue || !session.BookingDraft.CheckOutDate.HasValue)
            return null;

        try
        {
            var availableRooms = await _hotelData.GetAvailableRoomsAsync(
                hotelId,
                session.BookingDraft.CheckInDate.Value,
                session.BookingDraft.CheckOutDate.Value,
                session.BookingDraft.NumAdults ?? 2,
                session.BookingDraft.NumChildren ?? 0);

            var nights = (session.BookingDraft.CheckOutDate.Value - session.BookingDraft.CheckInDate.Value).Days;
            var suggestions = new List<RoomSuggestionDto>();

            foreach (var room in availableRooms.Take(5))
            {
                var totalPrice = await _hotelData.CalculatePriceAsync(
                    hotelId, room.RoomType,
                    session.BookingDraft.CheckInDate.Value,
                    session.BookingDraft.CheckOutDate.Value);

                var amenities = await _hotelData.GetAmenitiesForRoomAsync(hotelId, room.Amenities);

                suggestions.Add(new RoomSuggestionDto
                {
                    RoomId = room.RoomId,
                    RoomNumber = room.RoomNumber,
                    RoomType = room.RoomType,
                    RoomTypeName = room.RoomTypeName,
                    Description = room.ShortDescription,
                    PricePerNight = nights > 0 ? totalPrice / nights : totalPrice,
                    TotalPrice = totalPrice,
                    View = room.View,
                    BedType = room.BedType,
                    MaxOccupancy = room.MaxOccupancy,
                    KeyAmenities = amenities.Take(5).Select(a => a.VietnameseName).ToList()
                });
            }

            return suggestions;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildBookingDraftContext(BookingDraftDto draft)
    {
        var lines = new List<string>();

        if (draft.CheckInDate.HasValue)
            lines.Add($"- Ngày check-in: {draft.CheckInDate.Value:dd/MM/yyyy}");
        if (draft.CheckOutDate.HasValue)
            lines.Add($"- Ngày check-out: {draft.CheckOutDate.Value:dd/MM/yyyy}");
        if (draft.CheckInDate.HasValue && draft.CheckOutDate.HasValue)
            lines.Add($"- Số đêm: {(draft.CheckOutDate.Value - draft.CheckInDate.Value).Days} đêm");
        if (draft.NumAdults.HasValue)
            lines.Add($"- Số người lớn: {draft.NumAdults.Value}");
        if (draft.NumChildren is > 0)
            lines.Add($"- Số trẻ em: {draft.NumChildren.Value}");
        if (!string.IsNullOrEmpty(draft.RoomType))
            lines.Add($"- Loại phòng: {draft.RoomType}");
        if (!string.IsNullOrEmpty(draft.GuestName))
            lines.Add($"- Tên khách: {draft.GuestName}");
        if (!string.IsNullOrEmpty(draft.GuestPhone))
            lines.Add($"- Điện thoại: {draft.GuestPhone}");
        if (!string.IsNullOrEmpty(draft.GuestEmail))
            lines.Add($"- Email: {draft.GuestEmail}");
        if (draft.EstimatedTotal.HasValue)
            lines.Add($"- Tổng tiền ước tính: {draft.EstimatedTotal.Value:N0} VND");

        return lines.Count == 0 ? "" : string.Join("\n", lines);
    }

    private bool IsBookingDraftComplete(BookingDraftDto draft)
    {
        return !string.IsNullOrEmpty(draft.GuestName)
            && !string.IsNullOrEmpty(draft.GuestPhone)
            && draft.CheckInDate.HasValue
            && draft.CheckOutDate.HasValue
            && draft.NumAdults.HasValue
            && !string.IsNullOrEmpty(draft.RoomId);
    }

    private bool CanResolveRoom(BookingDraftDto draft)
    {
        return draft.CheckInDate.HasValue
            && draft.CheckOutDate.HasValue
            && draft.NumAdults.HasValue
            && !string.IsNullOrWhiteSpace(draft.RoomType);
    }

    private async Task TryResolveRoomIdAsync(ConversationSession session, string hotelId)
    {
        var draft = session.BookingDraft;

        if (!string.IsNullOrWhiteSpace(draft.RoomId) || !CanResolveRoom(draft))
            return;

        try
        {
            var availableRooms = await _hotelData.GetAvailableRoomsAsync(
                hotelId,
                draft.CheckInDate!.Value,
                draft.CheckOutDate!.Value,
                draft.NumAdults!.Value,
                draft.NumChildren ?? 0);

            if (!availableRooms.Any())
                return;

            var preferredType = NormalizeRoomValue(draft.RoomType!);
            var matchedRoom = availableRooms.FirstOrDefault(r =>
                   NormalizeRoomValue(r.RoomId) == preferredType
                || NormalizeRoomValue(r.RoomType) == preferredType
                || NormalizeRoomValue(r.RoomTypeName) == preferredType
                || NormalizeRoomValue(r.RoomTypeName).Contains(preferredType)
                || preferredType.Contains(NormalizeRoomValue(r.RoomType)))
                ?? availableRooms.FirstOrDefault(r =>
                    NormalizeRoomValue(r.RoomType) == preferredType);

            if (matchedRoom == null)
                return;

            draft.RoomId = matchedRoom.RoomId;
            draft.RoomType ??= matchedRoom.RoomType;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving room id for session {SessionId}", session.SessionId);
        }
    }

    private string BuildBookingPendingMessage(BookingDraftDto draft)
    {
        var missingFields = GetMissingBookingFields(draft);

        if (!missingFields.Any())
            return "Dạ em đã ghi nhận xác nhận đặt phòng của anh/chị, nhưng hiện chưa thể hoàn tất booking trên hệ thống. Anh/chị vui lòng thử lại trong giây lát nhé.";

        return $"Dạ em chưa thể chốt booking trên hệ thống vì còn thiếu: {string.Join(", ", missingFields)}. Anh/chị giúp em bổ sung để em tạo booking chính xác ngay nhé.";
    }

    private List<string> GetMissingBookingFields(BookingDraftDto draft)
    {
        var missing = new List<string>();

        if (!draft.CheckInDate.HasValue) missing.Add("ngày check-in");
        if (!draft.CheckOutDate.HasValue) missing.Add("ngày check-out");
        if (!draft.NumAdults.HasValue) missing.Add("số lượng khách");
        if (string.IsNullOrWhiteSpace(draft.RoomType) && string.IsNullOrWhiteSpace(draft.RoomId)) missing.Add("loại phòng");
        if (string.IsNullOrWhiteSpace(draft.GuestName)) missing.Add("họ tên");
        if (string.IsNullOrWhiteSpace(draft.GuestPhone)) missing.Add("số điện thoại");
        if (string.IsNullOrWhiteSpace(draft.RoomId) && !string.IsNullOrWhiteSpace(draft.RoomType)) missing.Add("phòng cụ thể còn trống phù hợp");

        return missing;
    }

    private static string NormalizeRoomValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var chars = value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private CreateBookingRequest BuildBookingRequest(ConversationSession session, string hotelId)
    {
        var draft = session.BookingDraft;
        return new CreateBookingRequest
        {
            SessionId = session.SessionId,
            HotelId = hotelId,
            GuestName = draft.GuestName!,
            GuestPhone = draft.GuestPhone!,
            GuestEmail = draft.GuestEmail,
            RoomId = draft.RoomId!,
            CheckInDate = draft.CheckInDate!.Value,
            CheckOutDate = draft.CheckOutDate!.Value,
            NumAdults = draft.NumAdults!.Value,
            NumChildren = draft.NumChildren ?? 0,
            SpecialRequests = draft.SpecialRequests,
            PromoCode = draft.PromoCode,
            PaymentMethod = "Cash"
        };
    }
}
