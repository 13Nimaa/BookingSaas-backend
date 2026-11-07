# Business gallery API — frontend guide

Optional multi-image gallery for a business. Separate from the single **profile** image (`POST /api/Business/image`).

**Status:** live on backend (migration `AddBusinessGallery` must be applied)  
**JSON:** camelCase

Related:

| Topic | Doc |
|-------|-----|
| Public detail (includes gallery) | `context/Business-Detail-API-frontend.md` |
| Public list / mine | `context/Business-List-API-frontend.md` |

---

## What to know

| Fact | Detail |
|------|--------|
| Create / register / update | Stay **JSON** — no files on those bodies |
| Upload gallery | `POST /api/Business/gallery` after create, or anytime on edit |
| Read gallery | `businessGallery` on `GET /api/Business/{id}` and on `BusinessDto` (list / mine) |
| Empty | Always an array — `[]` when none |
| Cap | **Max 10** images total per business |
| Formats | JPG / PNG / WebP, ≤ **5 MB** each |
| Field name | Form field must be **`files`** (repeat for each file) |

---

## Types

```ts
type BusinessGalleryImageDto = {
  url: string;
  publicId: string; // needed for DELETE
};

// Appears on BusinessDto + BusinessDetailsDto:
businessGallery: BusinessGalleryImageDto[];
```

---

## Endpoints

### Upload (append)

```http
POST /api/Business/gallery
Authorization: Bearer <accessToken>
Content-Type: multipart/form-data
```

| Part | Required | Notes |
|------|----------|-------|
| `files` | yes | One or more images; appends to existing gallery |

**Success `200`**
```json
{
  "message": "تصاویر گالری با موفقیت اضافه شد.",
  "businessGallery": [
    { "url": "https://…/a.jpg", "publicId": "booking-saas/businesses/gallery/…" }
  ]
}
```

**Errors**

| Status | When |
|--------|------|
| `400` | No files / bad type / over size / would exceed 10 |
| `401` | Missing/invalid token |
| `404` | Caller has no business |

### Delete one image

```http
DELETE /api/Business/gallery?publicId=<publicId>
Authorization: Bearer <accessToken>
```

**Success `200`** — same shape as upload (`message` + remaining `businessGallery`).

| Status | When |
|--------|------|
| `400` | Missing `publicId` |
| `404` | No business, or that `publicId` not in gallery |
| `500` | Removed from DB but Cloudinary delete failed (rare) |

### Read (public)

```http
GET /api/Business/{id}
```

Use `response.businessGallery` for a carousel / grid. No auth.

```ts
business.businessGallery.map((g) => g.url);
```

Owner edit screen can also load gallery from `GET /api/Business/mine` → `business.businessGallery`.

---

## Suggested UI flows

### Create / register business

```ts
// 1. Create business (JSON) — register or admin create
await registerBusiness({ name, city, address, description });

// 2. Optional: upload gallery right after
if (selectedFiles.length > 0) {
  await uploadGallery(accessToken, selectedFiles);
}
```

### Edit business

```ts
// 1. PUT /api/Business/{id} with text fields (JSON)
await updateBusiness(id, { name, city, address, description });

// 2. Add new images
await uploadGallery(accessToken, newlyPickedFiles);

// 3. Remove an image (use publicId from current gallery item)
await deleteGalleryImage(accessToken, image.publicId);
```

### Public business page

```ts
const business = await getBusinessDetail(businessId);

if (business.businessGallery.length === 0) {
  // hide gallery section
} else {
  // show images from business.businessGallery[].url
}
```

---

## Helpers

```ts
const API_BASE = process.env.NEXT_PUBLIC_API_BASE!; // e.g. http://localhost:5xxx

async function uploadGallery(accessToken: string, files: File[]) {
  const form = new FormData();
  for (const file of files) form.append("files", file);

  const res = await fetch(`${API_BASE}/api/Business/gallery`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}` },
    // do NOT set Content-Type — browser sets multipart boundary
    body: form,
  });

  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as {
    message: string;
    businessGallery: BusinessGalleryImageDto[];
  };
}

async function deleteGalleryImage(accessToken: string, publicId: string) {
  const qs = new URLSearchParams({ publicId });
  const res = await fetch(`${API_BASE}/api/Business/gallery?${qs}`, {
    method: "DELETE",
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as {
    message: string;
    businessGallery: BusinessGalleryImageDto[];
  };
}
```

**File picker tips**

- `accept="image/jpeg,image/png,image/webp"`
- Client-side: reject files &gt; 5 MB before upload
- Disable “Add” when `currentGallery.length >= 10`
- Prefer appending `files` once per file (`form.append("files", file)`), not a single blob

---

## Do not

- Put gallery files on `POST /register`, `POST /api/Business`, or `PUT /api/Business/{id}` — those stay JSON
- Confuse gallery with profile image (`POST /api/Business/image` replaces the single cover/avatar)
- Store only `url` in local state for delete — keep `publicId` too
