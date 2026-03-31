using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.RAG;

namespace HotelChatbot.Infrastructure.RAG.Services;

public class HotelKnowledgeDocumentFactory
{
    private readonly IGoogleSheetsService _sheets;

    public HotelKnowledgeDocumentFactory(IGoogleSheetsService sheets)
    {
        _sheets = sheets;
    }

    public async Task<List<KnowledgeDocument>> BuildAsync(string hotelId)
    {
        var documents = new List<KnowledgeDocument>();

        var hotel = await _sheets.GetHotelInfoAsync(hotelId);
        var rooms = await _sheets.GetRoomsAsync(hotelId);
        var amenities = await _sheets.GetAmenitiesAsync(hotelId);
        var promotions = await _sheets.GetActivePromotionsAsync(hotelId);
        var faqs = await _sheets.GetFAQsAsync(hotelId);

        if (hotel != null)
        {
            documents.AddRange(BuildHotelInfoDocuments(hotel));
        }

        documents.AddRange(rooms.Where(r => r.IsActive).Select(BuildRoomDocument));
        documents.AddRange(amenities.Where(a => a.IsActive).Select(a => BuildAmenityDocument(hotelId, a)));
        documents.AddRange(promotions.Where(p => p.IsActive).Select(BuildPromotionDocument));
        documents.AddRange(faqs.Where(f => f.IsActive).Select(f => BuildFaqDocument(hotelId, f)));

        return documents;
    }

    private static IEnumerable<KnowledgeDocument> BuildHotelInfoDocuments(HotelChatbot.Core.Models.HotelInfo hotel)
    {
        yield return new KnowledgeDocument
        {
            HotelId = hotel.HotelId,
            DocType = "hotel_info_overview",
            EntityType = "HotelInfo",
            EntityId = "overview",
            Title = $"{hotel.Name} - Overview",
            Text = $"Tên khách sạn: {hotel.Name}. Giới thiệu: {hotel.Description}. Xếp hạng: {hotel.StarRating} sao.",
            Tags = new() { "hotel", "overview" },
            SourceSheet = "HotelInfo",
            SourceRowKey = hotel.HotelId
        };

        yield return new KnowledgeDocument
        {
            HotelId = hotel.HotelId,
            DocType = "hotel_info_location",
            EntityType = "HotelInfo",
            EntityId = "location",
            Title = $"{hotel.Name} - Location",
            Text = $"Địa chỉ: {hotel.Address}. Vị trí: {hotel.LocationDescription}. Điểm lân cận: {hotel.NearbyLandmarks}.",
            Tags = new() { "hotel", "location" },
            SourceSheet = "HotelInfo",
            SourceRowKey = hotel.HotelId
        };

        yield return new KnowledgeDocument
        {
            HotelId = hotel.HotelId,
            DocType = "hotel_info_policies",
            EntityType = "HotelInfo",
            EntityId = "policies",
            Title = $"{hotel.Name} - Policies",
            Text = $"Check-in: {hotel.CheckInTime}. Check-out: {hotel.CheckOutTime}. Check-in sớm: {hotel.EarlyCheckInPolicy}. Check-out muộn: {hotel.LateCheckOutPolicy}. Hủy phòng: {hotel.CancellationPolicy}. Thú cưng: {hotel.PetPolicy}. Hút thuốc: {hotel.SmokingPolicy}. Trẻ em: {hotel.ChildPolicy}. Thanh toán: {hotel.PaymentMethods}.",
            Tags = new() { "hotel", "policy" },
            SourceSheet = "HotelInfo",
            SourceRowKey = hotel.HotelId
        };
    }

    private static KnowledgeDocument BuildRoomDocument(HotelChatbot.Core.Models.Room room)
    {
        return new KnowledgeDocument
        {
            HotelId = room.HotelId,
            DocType = "room",
            EntityType = "Room",
            EntityId = room.RoomId,
            Title = room.RoomTypeName,
            Text = $"Loại phòng: {room.RoomType}. Tên hiển thị: {room.RoomTypeName}. Phòng số: {room.RoomNumber}. Diện tích: {room.SizeM2}m2. Sức chứa: {room.MaxAdults} người lớn, {room.MaxChildren} trẻ em. Giường: {room.BedType}. View: {room.View}. Mô tả: {room.Description}. Tóm tắt: {room.ShortDescription}.",
            RoomType = room.RoomType,
            Tags = room.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            SourceSheet = "Rooms",
            SourceRowKey = room.RoomId
        };
    }

    private static KnowledgeDocument BuildAmenityDocument(string hotelId, HotelChatbot.Core.Models.Amenity amenity)
    {
        return new KnowledgeDocument
        {
            HotelId = hotelId,
            DocType = "amenity",
            EntityType = "Amenity",
            EntityId = amenity.AmenityId,
            Title = amenity.VietnameseName,
            Text = $"Tiện ích: {amenity.VietnameseName}. Tên tiếng Anh: {amenity.Name}. Nhóm: {amenity.Category}. Mô tả: {amenity.Description}. Miễn phí: {(amenity.IsComplimentary ? "Có" : "Không")}. Phí: {amenity.Fee?.ToString() ?? "0"}. Giờ hoạt động: {amenity.OperatingHours}.",
            Category = amenity.Category,
            Tags = new() { "amenity", amenity.Category.ToLowerInvariant() },
            SourceSheet = "Amenities",
            SourceRowKey = amenity.AmenityId
        };
    }

    private static KnowledgeDocument BuildPromotionDocument(HotelChatbot.Core.Models.Promotion promotion)
    {
        return new KnowledgeDocument
        {
            HotelId = promotion.HotelId,
            DocType = "promotion",
            EntityType = "Promotion",
            EntityId = promotion.PromoId,
            Title = promotion.Title,
            Text = $"Mã khuyến mãi: {promotion.PromoCode}. Tiêu đề: {promotion.Title}. Mô tả: {promotion.Description}. Loại giảm giá: {promotion.DiscountType}. Giá trị: {promotion.DiscountValue}. Số đêm tối thiểu: {promotion.MinimumStayNights}. Mức chi tối thiểu: {promotion.MinimumSpend}. Áp dụng cho: {promotion.ApplicableRoomTypes}. Hiệu lực từ {promotion.ValidFrom:yyyy-MM-dd} đến {promotion.ValidTo:yyyy-MM-dd}. Điều kiện: {promotion.Conditions}.",
            Tags = new() { "promotion" },
            SourceSheet = "Promotions",
            SourceRowKey = promotion.PromoId
        };
    }

    private static KnowledgeDocument BuildFaqDocument(string hotelId, HotelChatbot.Core.Models.FAQ faq)
    {
        return new KnowledgeDocument
        {
            HotelId = hotelId,
            DocType = "faq",
            EntityType = "FAQ",
            EntityId = faq.FaqId,
            Title = faq.Question,
            Text = $"Câu hỏi: {faq.Question}. Trả lời: {faq.Answer}. Từ khóa: {faq.Keywords}.",
            Category = faq.Category,
            Priority = faq.Priority,
            Keywords = faq.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Tags = new() { "faq", faq.Category.ToLowerInvariant() },
            SourceSheet = "FAQs",
            SourceRowKey = faq.FaqId
        };
    }
}
