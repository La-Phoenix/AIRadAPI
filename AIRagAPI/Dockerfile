# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy everything and restore + publish
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published output
COPY --from=build /out .

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose backend port
EXPOSE 8080

# Start backend
ENTRYPOINT ["dotnet", "AIRagAPI.dll"]