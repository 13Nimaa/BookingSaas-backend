# Business Dashboard — Backend Implementation

Implement the backend data aggregation required by the existing Business Owner Dashboard.

The frontend dashboard design is already implemented. Your task is to provide real, business-scoped dashboard data through the backend.

Use the existing domain model and database. Do not introduce unnecessary entities, database tables, or migrations.

## 1. Dashboard API

Create a dedicated dashboard endpoint:

`GET /api/business/dashboard`

Authorization:
- Only authenticated BusinessOwner users can access this endpoint.
- Resolve the authenticated owner from the existing JWT/Identity context.
- Resolve their owned business using the existing ownership relationship.
- Never accept BusinessId from the client as the source of authorization.
- Every aggregation and query must be scoped to the authenticated owner's business.
- Follow the existing BusinessController ownership and authorization conventions.

Return a single dashboard response containing the available summary metrics, weekly revenue data, today's bookings, and top-performing services.

Use appropriate DTOs and a dedicated dashboard service. Keep controllers thin.

## 2. Dashboard Summary Cards

The frontend currently displays four summary cards.

### A. Today's Bookings

Calculate the number of bookings scheduled for today.

Requirements:
- Use the business's local date, based on `Business.TimeZoneId`.
- Include bookings whose local StartTime falls on the current business date.
- Exclude cancelled and expired bookings from the active booking count.
- Use the actual existing BookingStatus enum values.
- Do not invent new statuses.

Return the total count.

### B. Today's Revenue

Important: The current domain has `Booking.Price` as a booking price snapshot, but no confirmed payment/transaction model.

Therefore, do not present this metric as actual collected or settled revenue.

Calculate the total value of bookings completed today, using the stored `Booking.Price` snapshot.

Requirements:
- Use the business's local date.
- Include only bookings with `Completed` status.
- Ignore null historical prices rather than treating them as zero.
- Use decimal arithmetic.
- Return null or an explicit unavailable state if the metric cannot be meaningfully calculated.

The frontend should identify this as estimated booking revenue/value, not confirmed payment revenue.

Do not add payment functionality or a payment entity.

### C. Customers

Calculate the total number of customers belonging to the authenticated business.

Requirements:
- Scope by BusinessId.
- Count actual Customer records.
- Do not count guest bookings as customers unless they already have a corresponding Customer record.
- Do not infer customer identity from matching phone numbers.

Return the total customer count.

### D. Booking Utilization / Capacity

The current dashboard reference includes a capacity percentage.

Only implement this metric if it can be calculated accurately from the existing data.

Potential data sources:
- Business working hours.
- Employee working hours.
- Employee-service assignments.
- Employee-specific service durations.
- Existing bookings and their time intervals.

If a reliable calculation is possible:
- Calculate available employee working minutes for the business-local current day.
- Calculate occupied minutes using valid booking intervals.
- Avoid double-counting overlapping intervals.
- Respect the actual working-hour intervals.
- Account for employee eligibility and assigned services.
- Exclude cancelled and expired bookings.
- Handle employees with no assigned services or no working hours correctly.

Return the percentage and the underlying occupied/available minutes.

If the current schema or business rules do not support a reliable calculation, do not return a fabricated percentage. Report exactly which data or rule is missing.

Do not add a new capacity model or change the availability system for this metric.

## 3. Weekly Revenue Chart

Provide the data required by the existing weekly revenue chart.

Requirements:
- Return the seven local calendar days of the current business week.
- Use the business timezone.
- Group bookings by their local date.
- Use only bookings with `Completed` status.
- Sum non-null `Booking.Price` snapshots.
- Return zero for days without completed bookings.
- Do not use current Service.Price to calculate historical booking values.
- Do not treat pending or confirmed bookings as completed revenue.
- Do not claim these values represent actual payments.

Each data point should include:
- Local date.
- Persian-friendly weekday identifier or a stable weekday value.
- Estimated completed booking value.
- Number of completed bookings.

Use decimal values for monetary amounts.

The response should contain all seven days in chronological order, including days with zero values.

## 4. Today's Booking List

Return a compact list of today's bookings for the dashboard widget.

Each item should contain only fields already available from the domain and existing relationships:

- Booking ID.
- Customer display name, when available.
- Service title.
- Employee name.
- Start time.
- End time.
- Booking status.
- Stored booking price, when available.

Requirements:
- Scope all records to the authenticated owner's business.
- Use business-local today's date.
- Exclude cancelled and expired bookings from the operational list.
- Order by StartTime ascending.
- Return a reasonable limited number of records for the dashboard widget.
- Include a total count if it can be obtained efficiently.
- Handle guest bookings without a linked Customer record using the existing guest information available in the booking model.
- Never fabricate missing customer or employee information.

Do not return full entity objects.

## 5. Top-Performing Services

Provide the data for the dashboard's service performance section.

Calculate service performance from actual bookings.

Requirements:
- Group bookings by ServiceId.
- Scope all bookings to the authenticated owner's business.
- Use completed bookings for a meaningful historical performance metric.
- Return the service title, booking count, and percentage of completed bookings across the returned service groups.
- Order by booking count descending.
- Return only the top services required by the dashboard.
- Calculate percentages from the actual aggregate counts.
- Avoid division by zero.
- Do not use current service prices to infer service revenue.

If the frontend needs today's service performance instead of historical performance, keep the query period explicit and consistent. Do not mix different reporting periods within the same response.

## 6. Date and Time Handling

This is important because businesses may operate in different timezones.

Use `Business.TimeZoneId` as the source of the business timezone.

Requirements:
- Determine the business-local current date.
- Convert local day/week boundaries to the appropriate UTC boundaries before querying UTC timestamps, if Booking timestamps are stored in UTC.
- Avoid using `DateTime.Now` or the server's local timezone for business reporting.
- Handle daylight-saving transitions correctly.
- Use the same date boundaries consistently across all dashboard widgets.

Do not change the existing booking timezone storage or availability behavior.

## 7. Query Performance

Dashboard data should be aggregated efficiently.

- Use database-side grouping, filtering, counting, and summation wherever practical.
- Avoid loading every Booking entity into memory to calculate aggregates.
- Avoid N+1 queries.
- Use AsNoTracking for read-only queries.
- Select only the columns needed for each widget.
- Avoid repeating expensive queries unnecessarily.
- Ensure all queries remain business-scoped.
- Consider combining related aggregations when it improves performance without making the code difficult to maintain.

Do not introduce caching or background jobs unless the existing project already has an established pattern for them.

## 8. Response DTO

Create a dedicated dashboard response structure with separate sections:

- Summary
- WeeklyRevenue
- TodayBookings
- TopServices
- Capacity, only if reliably supported

Use explicit nullable values where a metric is unavailable.

Keep the response predictable and easy for the existing Next.js frontend to consume.

Use decimal types for monetary values and appropriate integer/numeric types for counts and percentages.

Do not expose EF Core entities directly.

## 9. Authorization and Data Isolation

This is a critical requirement.

- A BusinessOwner must only see data belonging to their own business.
- Do not trust a client-provided BusinessId.
- Ensure all joined entities are constrained to the same business.
- Do not expose data belonging to another salon through bookings, customers, employees, or services.
- Follow existing authorization and ownership patterns.

## 10. Strict Constraints

- Do not modify frontend code.
- Do not modify existing booking creation or availability logic.
- Do not change Booking.Price snapshot behavior.
- Do not introduce a payment system.
- Do not create fake dashboard data.
- Do not add unnecessary domain entities.
- Do not add migrations unless a genuinely required schema change is identified and separately explained.
- Do not change existing BookingStatus semantics.
- Do not refactor unrelated controllers or services.

## 11. Implementation and Verification

Implement the dashboard endpoint, DTOs, service, dependency injection, and relevant tests.

Test at least:

1. BusinessOwner receives only their own business data.
2. Today's bookings respect the business timezone.
3. Cancelled and expired bookings are excluded from operational counts.
4. Completed booking values use Booking.Price snapshots.
5. Null historical prices are handled correctly.
6. Weekly aggregation returns exactly seven chronological days.
7. Days without completed bookings return zero.
8. Top services are grouped and ordered correctly.
9. Guest bookings are handled without assuming a Customer relationship.
10. Empty businesses return valid empty dashboard data.
11. Businesses in different timezones receive correct date boundaries.
12. Capacity is either accurately calculated or explicitly reported as unsupported.

Before implementing a metric, verify that the required source fields and relationships actually exist. If a metric cannot be calculated from the current schema, omit it and report the missing requirement instead of guessing.

Do not apply database migrations without explicit approval.

At completion, report:
- Endpoint and response DTO structure.
- Metrics implemented.
- Metrics omitted and why.
- Files changed.
- Tests and results.
- Whether any migration is required.

Implement only this dashboard backend task.