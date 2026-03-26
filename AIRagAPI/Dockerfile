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

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy backend from build
COPY --from=build /out .

# Install dependencies for Ollama
RUN apt-get update && apt-get install -y wget unzip curl \
    && rm -rf /var/lib/apt/lists/*

# Download and install Ollama CLI / server
RUN wget https://ollama.com/download/ollama-linux-latest.zip -O /tmp/ollama.zip \
    && unzip /tmp/ollama.zip -d /usr/local/bin \
    && chmod +x /usr/local/bin/ollama

# Create persistent directory for Ollama data
RUN mkdir -p /root/.ollama
VOLUME /root/.ollama

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose backend port (Railway uses this)
EXPOSE 8080
# Ollama internal port (optional if you need internal calls)
EXPOSE 11434

# Start Ollama in background, then start backend
ENTRYPOINT ["sh", "-c", "ollama serve & sleep 5 && exec dotnet AIRagAPI.dll"]