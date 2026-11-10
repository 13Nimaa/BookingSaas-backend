# BookingSaaS Backend

## Overview

BookingSaaS is a multi-tenant booking platform API for Iranian businesses (salons, clinics, and similar). Owners manage a business profile, employees, services, working hours, and customers. Customers and guests book appointments. This repo is the ASP.NET Core backend consumed by the Next.js frontend (`bookingsass-frontend`).

## Goals

1. Authenticate users with phone + password (JWT access + rotating refresh).
2. Let customers self-register a business and become `BusinessOwner`.
3. Let owners manage business profile, employees, services, employee↔service assignments, and working hours.
4. Provide per-business CRM for customers (registered and guest).
5. Provide a booking lifecycle (create, confirm, cancel, complete, expire) on the `Booking` model.
6. Expose stable HTTP contracts the frontend can wire without inventing shapes.

## Core User Flow (current)

1. User registers / logs in via Auth API.
2. User optionally upgrades to BusinessOwner via `POST /api/Business/register`.
3. Owner manages business, employees, services, employee↔service assignments, working hours, and CRM customers.
4. Public callers list/browse businesses; detail includes public-safe employees, assignments (effective price/duration), and upcoming working hours.
5. Customers/guests can query free slots via `GET /api/bookings/availability` and create Pending bookings via `POST /api/bookings` and `POST /api/bookings/guest` (availability is informational; create enforces no-overlap for Pending/Confirmed via Postgres exclusion — migration not applied outside tests). Owners can confirm/cancel/complete via `PATCH /api/bookings/{id}/status`. Owners and employees can list/filter bookings via `GET /api/bookings` and retrieve a single booking via `GET /api/bookings/{id}`. Authenticated customers can view their own booking history via `GET /api/bookings/mine`, open the customer home via `GET /api/customer/dashboard`, manage salon favorites, and self-cancel via `POST /api/bookings/{id}/cancel` (scoped by `Customer.UserId`). Pending bookings older than `Booking:PendingExpirationMinutes` (default 30) are auto-expired by a hosted background service.

## Features

### Authentication

- Phone as username; JWT access (~15 min) + hashed refresh tokens (cookie + body).
- Roles: `Customer`, `BusinessOwner`, `Employee`, `Admin` (Identity roles).

### Profile

- Get/update profile; Cloudinary profile image upload/delete.

### Business

- Public list; public detail with nested employees / assignments / hours / optional gallery; owner “mine”; update; profile image + gallery upload/delete.
- Self-register upgrades role to `BusinessOwner` (one business per owner).

### Employees & working hours

- Owner add/list/remove employees by existing user phone.
- Owner assign/list/remove services for an employee (`EmployeeServices`).
- Working hours CRUD by Gregorian `Date` (Jalali conversion is frontend).

### Services

- Owner CRUD for business services.

### Customers (CRM)

- Per-business customers: create, update, soft-deactivate, list/filter/page.
- Guest customers supported (`UserId` null).

### Bookings

- `Booking` entity + `BookingService` + `BookingController` create + availability endpoints.
- `IBookingService` registered in DI; create sets Pending + creator fields; EndTime from service duration.
- Availability returns free slots from employee working hours minus Pending/Confirmed bookings.
- Create overlap prevention via PostgreSQL exclusion constraint (Pending/Confirmed, half-open intervals).
- Status-transition API is live (`PATCH /api/bookings/{id}/status`).
- Pending auto-expiration via `PendingBookingExpirationService` (atomic Postgres UPDATE).
- Legacy `Appointment` entity remains unused by API — do not delete or dual-write without an explicit decision.

## Scope

### In Scope

- Auth, profile, business, employees, services, employee↔service assignments, working hours, customers
- Booking domain model and eventual booking HTTP API
- EF Core migrations (create in repo; apply only when explicitly asked)
- Integration tests with Testcontainers when requested

### Out Of Scope (for now)

- Payments, notifications, reviews, SaaS billing
- Admin panel product surface (Admin role exists for a few endpoints only)
- Replacing or removing `Appointment` without an explicit migrate plan

## Success Criteria

1. Authenticated owners can manage business, staff, services, hours, and CRM customers.
2. Public business discovery works without auth.
3. Booking eventually creates durable history with clear creator provenance and timezone-aware business context.
4. Changes respect layered architecture and existing naming.
5. Context docs (`progress-tracker`, inventory, issues) stay accurate after meaningful work.

## Stack Snapshot

| Layer | Technology |
| ----- | ---------- |
| Runtime | .NET 10 / ASP.NET Core |
| ORM | EF Core 10 + Npgsql (PostgreSQL) |
| Auth | ASP.NET Identity + JWT Bearer |
| Validation | FluentValidation |
| Media | Cloudinary |
| Tests | xUnit + WebApplicationFactory + Testcontainers |
| Frontend | External Next.js app (CORS `http://localhost:3000`) |
