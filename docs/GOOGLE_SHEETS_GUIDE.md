# 📊 GOOGLE SHEETS SETUP GUIDE

## Hướng dẫn tạo Google Sheet cho Hotel AI Chatbot

### BƯỚC 1: Tạo Google Spreadsheet mới
1. Vào https://sheets.google.com → Tạo spreadsheet mới
2. Đặt tên: "Hotel Chatbot Data - [Tên khách sạn]"
3. Copy Spreadsheet ID từ URL (chuỗi dài sau /d/ và trước /edit)

---

## CẤU TRÚC CÁC SHEET

### Sheet 1: HotelInfo
| Cột | Tên cột | Mô tả | Ví dụ |
|-----|---------|-------|-------|
| A | HotelId | Mã khách sạn | hotel_01 |
| B | Name | Tên khách sạn | Grand Palace Hotel Đà Nẵng |
| C | Description | Mô tả tổng quan | Khách sạn 4 sao sang trọng bên bờ biển... |
| D | Address | Địa chỉ đầy đủ | 123 Võ Nguyên Giáp, Đà Nẵng |
| E | LocationDescription | Mô tả vị trí, giao thông | Nằm ngay trung tâm, cách sân bay 15 phút... |
| F | PhoneNumber | Số điện thoại | 0236 123 4567 |
| G | Email | Email liên hệ | info@grandpalace.vn |
| H | Website | Website | www.grandpalace.vn |
| I | NearbyLandmarks | Địa điểm lân cận | Bãi biển Mỹ Khê 200m, Cầu Rồng 2km... |
| J | StarRating | Hạng sao | 4 |
| K | CheckInTime | Giờ check-in | 14:00 |
| L | CheckOutTime | Giờ check-out | 12:00 |
| M | EarlyCheckInPolicy | Chính sách check-in sớm | Phụ thu 50% giá phòng nếu check-in trước 10h |
| N | LateCheckOutPolicy | Chính sách check-out muộn | Phụ thu 50% nếu check-out sau 14h |
| O | CancellationPolicy | Chính sách hủy phòng | Hủy trước 24h miễn phí, sau đó thu 1 đêm |
| P | PetPolicy | Chính sách thú cưng | Không cho phép mang thú cưng |
| Q | SmokingPolicy | Chính sách hút thuốc | Cấm hút thuốc trong phòng, có khu vực riêng |
| R | ChildPolicy | Chính sách trẻ em | Trẻ dưới 6 tuổi miễn phí khi dùng giường cha mẹ |
| S | PaymentMethods | Phương thức thanh toán | Tiền mặt, thẻ Visa/Mastercard, chuyển khoản |
| T | Languages | Ngôn ngữ hỗ trợ | Tiếng Việt, English |
| U | SocialMedia | Mạng xã hội | Facebook: fb.com/grandpalace |

---

### Sheet 2: Rooms
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | HotelId | hotel_01 |
| B | RoomId | room_101 |
| C | RoomNumber | 101 |
| D | RoomType | Standard |
| E | RoomTypeName | Phòng Standard |
| F | Floor | 1 |
| G | SizeM2 | 28 |
| H | MaxAdults | 2 |
| I | MaxChildren | 1 |
| J | MaxOccupancy | 3 |
| K | BedType | Queen Size |
| L | View | Garden View |
| M | Description | Phòng tiêu chuẩn ấm cúng với đầy đủ tiện nghi hiện đại... |
| N | ShortDescription | Phòng Standard 28m², view vườn |
| O | Amenities | AM001,AM002,AM003 (ID từ sheet Amenities) |
| P | ImageUrls | https://... |
| Q | Tags | cozy,standard,garden |
| R | IsActive | TRUE |

**Các RoomType phổ biến:** Standard, Superior, Deluxe, Suite, Family, Penthouse

---

### Sheet 3: Pricing
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | HotelId | hotel_01 |
| B | PricingId | price_std_01 |
| C | RoomType | Standard |
| D | WeekdayPrice | 800000 |
| E | WeekendPrice | 1000000 |
| F | HolidayPrice | 1200000 |
| G | PeakSeasonPrice | 1500000 |
| H | Currency | VND |
| I | ExtraAdultFee | 200000 |
| J | ExtraChildFee | 100000 |
| K | BreakfastIncluded | No |
| L | BreakfastFee | 150000 |
| M | Notes | Giá đã bao gồm VAT |
| N | ValidFrom | 2025-01-01 |
| O | ValidTo | 2025-12-31 |

---

### Sheet 4: Availability
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | HotelId | hotel_01 |
| B | AvailabilityId | avail_001 |
| C | RoomId | room_101 |
| D | RoomNumber | 101 |
| E | Date | 2025-06-15 |
| F | Status | Booked *(Available/Booked/Maintenance/Blocked)* |
| G | BookingId | BK250615123456 |
| H | Notes | |

⚠️ **Lưu ý:** Phòng không có record trong sheet này = phòng Available. Chỉ cần ghi khi phòng KHÔNG available.

---

### Sheet 5: Amenities
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | AmenityId | AM001 |
| B | Name | Swimming Pool |
| C | VietnameseName | Hồ bơi ngoài trời |
| D | Category | Hotel *(InRoom/Hotel/Service/Recreation)* |
| E | Description | Hồ bơi ngoài trời 25m với view biển tuyệt đẹp |
| F | Icon | 🏊 |
| G | IsComplimentary | TRUE |
| H | Fee | 0 |
| I | OperatingHours | 6:00 - 22:00 |
| J | IsActive | TRUE |

**Gợi ý Amenities cần có:**
- InRoom: Điều hòa, TV, Minibar, Két sắt, Bồn tắm, Vòi hoa sen, Máy sấy tóc, Wifi, Ban công
- Hotel: Hồ bơi, Gym, Spa, Nhà hàng, Quầy bar, Sảnh chờ, Bãi đỗ xe, Thang máy
- Service: Lễ tân 24/7, Dọn phòng hàng ngày, Đặt tour, Thuê xe, Giặt ủi
- Recreation: Phòng game, Khu vui chơi trẻ em, Sân tennis

---

### Sheet 6: Promotions
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | HotelId | hotel_01 |
| B | PromoId | promo_001 |
| C | PromoCode | SUMMER25 |
| D | Title | Ưu đãi Hè 2025 |
| E | Description | Giảm 25% khi đặt phòng hè |
| F | DiscountType | Percentage *(Percentage/FixedAmount/FreeNight)* |
| G | DiscountValue | 25 |
| H | MinimumStayNights | 2 |
| I | MinimumSpend | 0 |
| J | ApplicableRoomTypes | All *(hoặc: Standard,Deluxe)* |
| K | ValidFrom | 2025-06-01 |
| L | ValidTo | 2025-08-31 |
| M | BookingWindow | |
| N | Conditions | Không áp dụng ngày lễ |
| O | IsActive | TRUE |
| P | UsageLimit | 0 *(0 = không giới hạn)* |
| Q | UsageCount | 0 |

---

### Sheet 7: Bookings *(Hệ thống tự ghi)*
| Cột | Tên cột |
|-----|---------|
| A | HotelId |
| B | BookingId |
| C | GuestName |
| D | GuestPhone |
| E | GuestEmail |
| F | GuestIdCard |
| G | Nationality |
| H | RoomId |
| I | RoomNumber |
| J | RoomType |
| K | CheckInDate |
| L | CheckOutDate |
| M | TotalNights |
| N | NumAdults |
| O | NumChildren |
| P | SpecialRequests |
| Q | RoomRate |
| R | TotalAmount |
| S | PromoCode |
| T | DiscountAmount |
| U | FinalAmount |
| V | BreakfastIncluded |
| W | PaymentMethod |
| X | PaymentStatus |
| Y | BookingStatus |
| Z | BookingChannel |
| AA | SessionId |
| AB | CreatedAt |
| AC | UpdatedAt |
| AD | Notes |

---

### Sheet 8: FAQs
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | FaqId | faq_001 |
| B | Category | CheckIn |
| C | Question | Check-in lúc mấy giờ? |
| D | Answer | Giờ check-in tiêu chuẩn là 14:00. Anh/chị có thể... |
| E | Keywords | checkin, nhận phòng, giờ |
| F | Priority | 1 |
| G | IsActive | TRUE |

---

### Sheet 9: Holidays
| Cột | Tên cột | Ví dụ |
|-----|---------|-------|
| A | HolidayId | hol_001 |
| B | Name | Tết Nguyên Đán |
| C | Date | 2025-01-29 |
| D | PriceType | Holiday |
| E | Notes | Tết Ất Tỵ |

---

## BƯỚC 2: Cấp quyền Service Account
1. Vào Google Cloud Console → tạo Service Account
2. Tải file JSON credentials
3. Copy email service account (dạng: name@project.iam.gserviceaccount.com)
4. Mở Google Sheet → Share → Dán email service account → Editor

## BƯỚC 3: Cấu hình trong appsettings.json
```json
{
  "GoogleSheets": {
    "SpreadsheetId": "1ABC...XYZ",
    "CredentialsPath": "credentials/service-account.json"
  }
}
```