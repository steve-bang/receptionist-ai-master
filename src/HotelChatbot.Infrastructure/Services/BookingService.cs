using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.Models;
using Microsoft.Extensions.Logging;

namespace HotelChatbot.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly IGoogleSheetsService _sheets;
    private readonly IHotelDataService _hotelData;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IGoogleSheetsService sheets,
        IHotelDataService hotelData,
        ILogger<BookingService> logger)
    {
        _sheets = sheets;
        _hotelData = hotelData;
        _logger = logger;
    }

    public string GenerateBookingId()
    {
        var timestamp = DateTime.Now.ToString("yyMMddHHmm");
        var random = new Random().Next(1000, 9999);
        return $"BK{timestamp}{random}";
    }

    public async Task<bool> ValidateBookingDataAsync(CreateBookingRequest request)
    {
        if (string.IsNullOrEmpty(request.GuestName)) throw new ArgumentException("Vui lòng cung cấp tên khách hàng");
        if (string.IsNullOrEmpty(request.GuestPhone)) throw new ArgumentException("Vui lòng cung cấp số điện thoại");
        if (request.CheckInDate >= request.CheckOutDate) throw new ArgumentException("Ngày check-out phải sau ngày check-in");
        if (request.CheckInDate < DateTime.Today) throw new ArgumentException("Ngày check-in không thể trong quá khứ");
        if (request.NumAdults < 1) throw new ArgumentException("Phải có ít nhất 1 người lớn");

        // Check room availability
        var availableRooms = await _hotelData.GetAvailableRoomsAsync(
            request.HotelId, request.CheckInDate, request.CheckOutDate,
            request.NumAdults, request.NumChildren);

        if (!availableRooms.Any(r => r.RoomId == request.RoomId))
            throw new InvalidOperationException("Phòng đã được đặt trong thời gian này. Vui lòng chọn phòng khác.");

        return true;
    }

    public async Task<BookingConfirmationDto> CreateBookingAsync(CreateBookingRequest request)
    {
        await ValidateBookingDataAsync(request);

        var rooms = await _sheets.GetRoomsAsync(request.HotelId);
        var room = rooms.FirstOrDefault(r => r.RoomId == request.RoomId)
            ?? throw new InvalidOperationException("Không tìm thấy thông tin phòng");

        var totalNights = (request.CheckOutDate - request.CheckInDate).Days;
        var totalPrice = await _hotelData.CalculatePriceAsync(
            request.HotelId, room.RoomType, request.CheckInDate, request.CheckOutDate);

        var discountAmount = 0m;
        if (!string.IsNullOrEmpty(request.PromoCode))
        {
            discountAmount = await _hotelData.ApplyPromoCodeAsync(
                request.HotelId, request.PromoCode, room.RoomType, totalPrice);
        }

        var hotel = await _hotelData.GetHotelInfoAsync(request.HotelId);
        var bookingId = GenerateBookingId();
        var now = DateTime.Now;

        var booking = new Booking
        {
            BookingId = bookingId,
            HotelId = request.HotelId,
            GuestName = request.GuestName,
            GuestPhone = request.GuestPhone,
            GuestEmail = request.GuestEmail ?? "",
            GuestIdCard = request.GuestIdCard ?? "",
            Nationality = "Vietnamese",
            RoomId = request.RoomId,
            RoomNumber = room.RoomNumber,
            RoomType = room.RoomType,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            TotalNights = totalNights,
            NumAdults = request.NumAdults,
            NumChildren = request.NumChildren,
            SpecialRequests = request.SpecialRequests ?? "",
            RoomRate = totalPrice / totalNights,
            TotalAmount = totalPrice,
            PromoCode = request.PromoCode ?? "",
            DiscountAmount = discountAmount,
            FinalAmount = totalPrice - discountAmount,
            BreakfastIncluded = request.BreakfastIncluded,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = "Pending",
            BookingStatus = "Confirmed",
            BookingChannel = "Chatbot",
            SessionId = request.SessionId,
            CreatedAt = now,
            UpdatedAt = now,
            Notes = ""
        };

        // Save to Google Sheets
        await _sheets.CreateBookingAsync(booking);

        // Update availability
        await _sheets.UpdateAvailabilityAsync(
            request.HotelId, request.RoomId,
            request.CheckInDate, request.CheckOutDate, bookingId);

        var confirmation = new BookingConfirmationDto
        {
            BookingId = bookingId,
            GuestName = booking.GuestName,
            RoomTypeName = room.RoomTypeName,
            RoomNumber = room.RoomNumber,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            TotalNights = totalNights,
            NumAdults = booking.NumAdults,
            NumChildren = booking.NumChildren,
            RoomRate = booking.RoomRate,
            DiscountAmount = discountAmount,
            FinalAmount = booking.FinalAmount,
            BreakfastIncluded = booking.BreakfastIncluded,
            PaymentMethod = booking.PaymentMethod,
            CheckInTime = hotel?.CheckInTime ?? "14:00",
            CheckOutTime = hotel?.CheckOutTime ?? "12:00",
            HotelPhone = hotel?.PhoneNumber ?? "",
            HotelAddress = hotel?.Address ?? "",
            ConfirmationMessage = BuildConfirmationMessage(booking, room, hotel)
        };

        _logger.LogInformation("Booking {BookingId} created for {GuestName}", bookingId, booking.GuestName);
        return confirmation;
    }

    private string BuildConfirmationMessage(Booking booking, Room room, HotelInfo? hotel)
    {
        return $"""
        ✅ ĐẶT PHÒNG THÀNH CÔNG!

        📋 Mã đặt phòng: **{booking.BookingId}**
        👤 Khách hàng: {booking.GuestName}
        📞 Điện thoại: {booking.GuestPhone}
        
        🏨 Phòng: {room.RoomTypeName} ({room.RoomNumber})
        🛏 Giường: {room.BedType}
        
        📅 Check-in: {booking.CheckInDate:dd/MM/yyyy} ({hotel?.CheckInTime ?? "14:00"})
        📅 Check-out: {booking.CheckOutDate:dd/MM/yyyy} ({hotel?.CheckOutTime ?? "12:00"})
        🌙 Số đêm: {booking.TotalNights} đêm
        👥 Khách: {booking.NumAdults} người lớn{(booking.NumChildren > 0 ? $", {booking.NumChildren} trẻ em" : "")}
        
        💰 Tổng tiền phòng: {booking.TotalAmount:N0} VND
        {(booking.DiscountAmount > 0 ? $"🎁 Giảm giá: -{booking.DiscountAmount:N0} VND\n" : "")}💳 Thành tiền: **{booking.FinalAmount:N0} VND**
        🍳 Bữa sáng: {(booking.BreakfastIncluded ? "Đã bao gồm ✓" : "Không bao gồm")}
        
        📍 {hotel?.Address}
        ☎ Liên hệ: {hotel?.PhoneNumber}
        
        Cảm ơn anh/chị đã chọn {hotel?.Name}! Em rất vui được đón tiếp anh/chị. Nếu cần hỗ trợ thêm, anh/chị liên hệ em nhé! 😊🌟
        """;
    }

    public async Task<Booking?> GetBookingByIdAsync(string bookingId)
        => await _sheets.GetBookingAsync(bookingId);

    public async Task<bool> CancelBookingAsync(string bookingId, string reason)
        => await _sheets.CancelBookingAsync(bookingId, reason);
}