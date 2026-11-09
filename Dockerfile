# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json + .csproj primeiro: a camada do restore só é refeita quando as dependências mudam.
COPY global.json ScrumRetroApi.csproj ./
RUN dotnet restore ScrumRetroApi.csproj

COPY . .
RUN dotnet publish ScrumRetroApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Porta padrão do Render (10000). DOTNET_gcConcurrent=false reduz memória/threads de GC (instância Free: 512 MB, 0,1 CPU).
ENV ASPNETCORE_HTTP_PORTS=10000 \
    DOTNET_gcConcurrent=false

COPY --from=build /app/publish .

EXPOSE 10000

# Usuário sem privilégios (já existe na imagem oficial); a porta 10000 não exige root.
USER $APP_UID

ENTRYPOINT ["dotnet", "ScrumRetroApi.dll"]
