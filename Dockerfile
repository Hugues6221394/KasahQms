# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files (for caching)
COPY KasahQMS.sln ./
COPY Core/KasahQMS.Domain/KasahQMS.Domain.csproj Core/KasahQMS.Domain/
COPY Core/KasahQMS.Application/KasahQMS.Application.csproj Core/KasahQMS.Application/
COPY Infrastructure/KasahQMS.Infrastructure/KasahQMS.Infrastructure.csproj Infrastructure/KasahQMS.Infrastructure/
COPY Infrastructure/KasahQMS.Infrastructure.Persistence/KasahQMS.Infrastructure.Persistence.csproj Infrastructure/KasahQMS.Infrastructure.Persistence/
COPY Presentation/KasahQMS.Web/KasahQMS.Web.csproj Presentation/KasahQMS.Web/
COPY Presentation/KasahQMS.Api/KasahQMS.Api.csproj Presentation/KasahQMS.Api/

# Restore ONLY the web project (avoids missing test projects issue)
RUN dotnet restore Presentation/KasahQMS.Web/KasahQMS.Web.csproj

# Copy everything
COPY . .

# Publish
RUN dotnet publish Presentation/KasahQMS.Web/KasahQMS.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl (for health checks)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user (security)
RUN groupadd -r kasahqms && useradd -r -g kasahqms -d /app -s /sbin/nologin kasahqms

# Create app directories
RUN mkdir -p /app/storage /app/logs && chown -R kasahqms:kasahqms /app

# Copy published output
COPY --from=build /app/publish .

# Fix permissions
RUN chown -R kasahqms:kasahqms /app

# Expose port
EXPOSE 8080

# Environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check (Render uses this)
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Switch to non-root user LAST
USER kasahqms

# Start app
ENTRYPOINT ["dotnet", "KasahQMS.Web.dll"]