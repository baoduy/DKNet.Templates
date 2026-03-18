ARG BUILDPLATFORM
ARG TARGETPLATFORM

# Use the official .NET 9 SDK (Alpine) image to build the app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy the rest of the source code
COPY . .

# Restore the dependencies
RUN dotnet restore Minimal.Api/Minimal.Api.csproj

# Build the app
RUN dotnet publish Minimal.Api/Minimal.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET 9 runtime (Alpine) image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app
RUN mkdir -p /app/data && chown -R 1000:1000 /app/data && chown -R appuser /app/data

# Switch to non-root user
USER appuser

# Set the entrypoint
ENTRYPOINT ["dotnet", "Minimal.Api.dll"]
