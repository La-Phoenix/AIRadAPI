#Development
# Build stage
#FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
#WORKDIR /app
#
#COPY . ./
#RUN dotnet restore
#RUN dotnet publish -c Release -o /out
#
## Runtime stage
#FROM mcr.microsoft.com/dotnet/aspnet:9.0
#WORKDIR /app
#
#COPY --from=build /out .
#
#ENV ASPNETCORE_ENVIRONMENT=Docker
#
##Development
##ENV ASPNETCORE_URLS=http://+:5000
##
##EXPOSE 5000
#
##Production
#ENV ASPNETCORE_URLS=http://+:8080
#EXPOSE 8080
#
#ENTRYPOINT ["dotnet", "AIRagAPI.dll"]

## Build stage
#FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
#WORKDIR /app
#
## Copy everything and restore + publish
#COPY . ./
#RUN dotnet restore
#RUN dotnet publish -c Release -o /out
#
## Runtime stage
#FROM mcr.microsoft.com/dotnet/aspnet:9.0
#WORKDIR /app
#
## Copy published output
#COPY --from=build /out .
#
## Environment variables
#ENV ASPNETCORE_ENVIRONMENT=Development
#ENV ASPNETCORE_URLS=http://+:5000
#
## Expose backend port
#EXPOSE 5000
#
## Start backend
#ENTRYPOINT ["dotnet", "AIRagAPI.dll"]

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