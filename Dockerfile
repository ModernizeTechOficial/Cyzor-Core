# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 as builder
WORKDIR /src

COPY ["Cyzor.Contracts/Cyzor.Contracts.csproj", "Cyzor.Contracts/"]
COPY ["Cyzor.Core/Cyzor.Core.csproj", "Cyzor.Core/"]
COPY ["Cyzor.Infrastructure/Cyzor.Infrastructure.csproj", "Cyzor.Infrastructure/"]
COPY ["Cyzor.Provisioning/Cyzor.Provisioning.csproj", "Cyzor.Provisioning/"]

RUN dotnet restore "Cyzor.Provisioning/Cyzor.Provisioning.csproj"

COPY . .
WORKDIR "/src/Cyzor.Provisioning"

RUN dotnet build "Cyzor.Provisioning.csproj" -c Release -o /app/build
RUN dotnet publish "Cyzor.Provisioning.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Instalar dependências necessárias
RUN apt-get update && apt-get install -y \
    curl \
    npm \
    gnupg \
    ca-certificates \
    && npm install -g pm2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=builder /app/publish .

# Copiar blueprints durante o build
COPY blueprints/node/ /var/www/builds/node/

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Criar estrutura de diretórios necessária
RUN mkdir -p /var/www/cyzor /var/www/letsencrypt && \
    chmod -R 755 /var/www

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "Cyzor.Provisioning.dll"]
