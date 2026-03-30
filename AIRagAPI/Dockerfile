# -----------------------------
# Build stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set working directory
WORKDIR /app

# Copy the project file and restore dependencies
COPY AIRagAPI/AIRagAPI.csproj ./
RUN dotnet restore

# Copy all other files
COPY AIRagAPI/. ./

# Publish the app to /out folder
RUN dotnet publish -c Release -o /out

# -----------------------------
# Runtime stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Copy published files from build stage
COPY --from=build /out .

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose the port the app will run on
EXPOSE 8080

# Start the app
ENTRYPOINT ["dotnet", "AIRagAPI.dll"]