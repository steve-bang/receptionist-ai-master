# InterContinental Nha Trang seed data

Files in this folder follow the schema in `docs/GOOGLE_SHEETS_GUIDE.md` and are ready to import into Google Sheets.

Hotel chosen
- InterContinental Nha Trang (real hotel in Nha Trang, Khanh Hoa)

What is factual
- Hotel identity, address, contact, check-in/out, pet policy, parking, Wi-Fi, room categories, room sizes, occupancy style, and major amenities were taken from public hotel pages.
- FAQ content was written from those same public facts.

What is seeded for operations
- `Pricing.csv` uses realistic demo prices so the chatbot can quote rates in development. These are not guaranteed live sell rates.
- `Availability.csv`, `Promotions.csv`, and `Bookings.csv` are intentionally empty except for headers, so the system starts in a clean state.

Suggested next step
1. Create a new Google Spreadsheet.
2. Create sheets with the same names as these CSV files.
3. Import each CSV into its matching tab.
4. Put the resulting Spreadsheet ID into `appsettings.json`.

Primary public sources used
- Official contact and hotel detail: https://www.ihg.com/intercontinental/hotels/us/en/nha-trang/nhach/hoteldetail
- Official contact/location page: https://nhatrang.intercontinental.com/contact-us/
- Official amenities page: https://www.ihg.com/intercontinental/hotels/us/en/nha-trang/nhach/hoteldetail/amenities
- Official room pages:
  - https://nhatrang.intercontinental.com/rooms/1-king-classic-ocean-view/
  - https://nhatrang.intercontinental.com/rooms/2-single-classic-ocean-view-high-floor/
  - https://nhatrang.intercontinental.com/rooms/1-king-classic-club-lounge-access-ocean-view/
  - https://nhatrang.intercontinental.com/rooms/1-king-junior-suite-ocean-view/
  - https://nhatrang.intercontinental.com/rooms/1-king-junior-suite-ocean-view-high-floor/
  - https://nhatrang.intercontinental.com/rooms/1-bedroom-suite-panorama-ocean-view/
- Additional public house-rules reference: https://www.booking.com/hotel/vn/intercontinental-nha-trang.html
- Market-rate reference used only to seed demo pricing bands: https://www.vietnamresorts.com/resort/intercontinental-nha-trang-an-ihg-hotel
