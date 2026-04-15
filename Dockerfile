# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY KasahQMS.sln ./
COPY Core/KasahQMS.Domain/KasahQMS.Domain.csproj Core/KasahQMS.Domain/
COPY Core/KasahQMS.Application/KasahQMS.Application.csproj Core/KasahQMS.Application/
COPY Infrastructure/KasahQMS.Infrastructure/KasahQMS.Infrastructure.csproj Infrastructure/KasahQMS.Infrastructure/
COPY Infrastructure/KasahQMS.Infrastructure.Persistence/KasahQMS.Infrastructure.Persistence.csproj Infrastructure/KasahQMS.Infrastructure.Persistence/
COPY Presentation/KasahQMS.Web/KasahQMS.Web.csproj Presentation/KasahQMS.Web/
COPY Presentation/KasahQMS.Api/KasahQMS.Api.csproj Presentation/KasahQMS.Api/

# Restore dependencies
RUN dotnet restore Presentation/KasahQMS.Web/KasahQMS.Web.csproj

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish Presentation/KasahQMS.Web/KasahQMS.Web.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r kasahqms && useradd -r -g kasahqms -d /app -s /sbin/nologin kasahqms

# Create directories for file storage and logs
RUN mkdir -p /app/storage /app/logs && \
    chown -R kasahqms:kasahqms /app

COPY --from=build /app/publish .
RUN chown -R kasahqms:kasahqms /app

# Switch to non-root user
USER kasahqms

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "KasahQMS.Web.dll"]
