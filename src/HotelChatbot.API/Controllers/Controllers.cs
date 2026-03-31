using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotelChatbot.API.Controllers;

// ============================================================
// CHAT CONTROLLER
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IConversationService _conversation;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IConversationService conversation, ILogger<ChatController> logger)
    {
        _conversation = conversation;
        _logger = logger;
    }

    /// <summary>
    /// Gửi tin nhắn tới chatbot AI lễ tân khách sạn
    /// </summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ChatResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Tin nhắn không được để trống" });

        try
        {
            var response = await _conversation.ProcessMessageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for session {SessionId}", request.SessionId);
            return StatusCode(500, new { error = "Đã xảy ra lỗi. Vui lòng thử lại." });
        }
    }

    /// <summary>
    /// Tạo session mới
    /// </summary>
    [HttpPost("session")]
    public IActionResult CreateSession([FromQuery] string hotelId = "default")
    {
        var sessionId = Guid.NewGuid().ToString();
        _conversation.GetOrCreateSession(sessionId, hotelId);
        return Ok(new { sessionId, hotelId, createdAt = DateTime.UtcNow });
    }
}

// ============================================================
// BOOKING CONTROLLER
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingController> _logger;

    public BookingController(IBookingService bookingService, ILogger<BookingController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Đặt phòng trực tiếp (không qua chatbot)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingConfirmationDto), 200)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        try
        {
            var confirmation = await _bookingService.CreateBookingAsync(request);
            return Ok(confirmation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, new { error = "Không thể tạo đặt phòng. Vui lòng thử lại." });
        }
    }

    /// <summary>
    /// Tra cứu đặt phòng theo mã
    /// </summary>
    [HttpGet("{bookingId}")]
    public async Task<IActionResult> GetBooking(string bookingId)
    {
        var booking = await _bookingService.GetBookingByIdAsync(bookingId);
        if (booking == null) return NotFound(new { error = "Không tìm thấy đặt phòng" });
        return Ok(booking);
    }

    /// <summary>
    /// Hủy đặt phòng
    /// </summary>
    [HttpDelete("{bookingId}")]
    public async Task<IActionResult> CancelBooking(string bookingId, [FromQuery] string reason = "Khách yêu cầu hủy")
    {
        var result = await _bookingService.CancelBookingAsync(bookingId, reason);
        if (!result) return NotFound(new { error = "Không tìm thấy đặt phòng" });
        return Ok(new { message = "Đã hủy đặt phòng thành công", bookingId });
    }
}

// ============================================================
// AVAILABILITY CONTROLLER
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AvailabilityController : ControllerBase
{
    private readonly IHotelDataService _hotelData;

    public AvailabilityController(IHotelDataService hotelData)
    {
        _hotelData = hotelData;
    }

    /// <summary>
    /// Kiểm tra phòng trống
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckAvailability([FromQuery] AvailabilityRequest request)
    {
        if (request.CheckInDate >= request.CheckOutDate)
            return BadRequest(new { error = "Ngày check-out phải sau ngày check-in" });

        var availableRooms = await _hotelData.GetAvailableRoomsAsync(
            request.HotelId, request.CheckInDate, request.CheckOutDate,
            request.NumAdults, request.NumChildren);

        var roomDtos = new List<AvailableRoomDto>();
        foreach (var room in availableRooms)
        {
            if (!string.IsNullOrEmpty(request.PreferredRoomType) &&
                room.RoomType != request.PreferredRoomType) continue;

            var totalPrice = await _hotelData.CalculatePriceAsync(
                request.HotelId, room.RoomType, request.CheckInDate, request.CheckOutDate);

            var amenities = await _hotelData.GetAmenitiesForRoomAsync(request.HotelId, room.Amenities);

            var pricing = await _hotelData.GetPricingForRoomAsync(
                request.HotelId, room.RoomType, request.CheckInDate, request.CheckOutDate);

            roomDtos.Add(new AvailableRoomDto
            {
                RoomId = room.RoomId,
                RoomNumber = room.RoomNumber,
                RoomType = room.RoomType,
                RoomTypeName = room.RoomTypeName,
                Description = room.Description,
                ShortDescription = room.ShortDescription,
                SizeM2 = room.SizeM2,
                MaxOccupancy = room.MaxOccupancy,
                BedType = room.BedType,
                View = room.View,
                PricePerNight = (request.CheckOutDate - request.CheckInDate).Days > 0
                    ? totalPrice / (request.CheckOutDate - request.CheckInDate).Days
                    : totalPrice,
                TotalPrice = totalPrice,
                Amenities = amenities.Select(a => a.VietnameseName).ToList(),
                BreakfastIncluded = pricing?.BreakfastIncluded == "Yes"
            });
        }

        return Ok(new AvailabilityResponse
        {
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            TotalNights = (request.CheckOutDate - request.CheckInDate).Days,
            AvailableRooms = roomDtos
        });
    }
}

// ============================================================
// HOTEL INFO CONTROLLER
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HotelController : ControllerBase
{
    private readonly IHotelDataService _hotelData;

    public HotelController(IHotelDataService hotelData)
    {
        _hotelData = hotelData;
    }

    [HttpGet("{hotelId}/info")]
    public async Task<IActionResult> GetHotelInfo(string hotelId = "default")
    {
        var info = await _hotelData.GetHotelInfoAsync(hotelId);
        if (info == null) return NotFound();
        return Ok(info);
    }

    [HttpGet("{hotelId}/promotions")]
    public async Task<IActionResult> GetPromotions(string hotelId = "default",
        [FromQuery] string? roomType = null,
        [FromQuery] DateTime? checkIn = null,
        [FromQuery] DateTime? checkOut = null)
    {
        var ci = checkIn ?? DateTime.Today;
        var co = checkOut ?? DateTime.Today.AddDays(1);
        var nights = (co - ci).Days;

        var promos = await _hotelData.GetApplicablePromotionsAsync(
            hotelId, roomType ?? "All", ci, co, nights);
        return Ok(promos);
    }
}