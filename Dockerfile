FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl FIRST (as root)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r kasahqms && useradd -r -g kasahqms -d /app -s /sbin/nologin kasahqms

# Create directories
RUN mkdir -p /app/storage /app/logs && \
    chown -R kasahqms:kasahqms /app

COPY --from=build /app/publish .
RUN chown -R kasahqms:kasahqms /app

# Switch to non-root AFTER everything
USER kasahqms

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "KasahQMS.Web.dll"]