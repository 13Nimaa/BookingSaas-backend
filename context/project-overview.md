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
4. Public callers list/browse businesses.
5. Customers/guests can create Pending bookings via `POST /api/bookings` and `POST /api/bookings/guest` (no availability/overlap yet). Domain foundation migration exists (not applied outside tests).

## Features

### Authentication

- Phone as username; JWT access (~15 min) + hashed refresh tokens (cookie + body).
- Roles: `Customer`, `BusinessOwner`, `Employee`, `Admin` (Identity roles).

### Profile

- Get/update profile; Cloudinary profile image upload/delete.

### Business

- Public list/detail; owner “mine”; update; image upload/delete.
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

- `Booking` entity + `BookingService` + `BookingController` create endpoints (authenticated + guest).
- `IBookingService` registered in DI; create sets Pending + creator fields; EndTime from service duration.
- Availability, overlap prevention, expiration, and status transitions are later phases.
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
- Availability engine / booking expiration BackgroundService (later phases)
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
