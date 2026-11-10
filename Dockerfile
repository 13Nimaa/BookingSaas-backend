# BookingSaaS API — multi-stage production image (.NET 10)
# Build:  docker build -t bookingsaas-api .
# Run:    docker run --rm -p 8080:8080 \
#           -e ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=BookingSaaS;Username=...;Password=..." \
#           -e Jwt__Key="<long-random-secret>" \
#           -e Jwt__Issuer=BookingSaaS \
#           -e Jwt__Audience=BookingSaaS \
#           -e Frontend__Origins__0="https://your-frontend.example" \
#           -e Cloudinary__CloudName=... \
#           -e Cloudinary__ApiKey=... \
#           -e Cloudinary__ApiSecret=... \
#           bookingsaas-api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BookingSaas-backend.csproj ./
RUN dotnet restore BookingSaas-backend.csproj

COPY . ./
RUN dotnet publish BookingSaas-backend.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Railway injects PORT at runtime; default 8080 for local docker runs.
ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=8080

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

# Bind to Railway's $PORT (not a baked ASPNETCORE_URLS) or the proxy returns 502,
# which browsers mis-report as a CORS error.
ENTRYPOINT ["sh", "-c", "dotnet BookingSaas-backend.dll --urls http://0.0.0.0:${PORT:-8080}"]
