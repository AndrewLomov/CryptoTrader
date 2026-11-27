# Build context is the repository root. Do not copy deploy/.env into this image.
# Build: docker build -f deploy/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/

# Host bin/obj from a developer machine must not replace the Linux publish output.
RUN find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + \
    && dotnet publish src/CryptoTrader.Host/CryptoTrader.Host.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# $APP_UID is the non-root user "app" built into the aspnet image.
USER $APP_UID

ENTRYPOINT ["dotnet", "CryptoTrader.Host.dll"]
