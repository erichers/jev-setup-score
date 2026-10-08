FROM node:22.22.3-bookworm AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY JevSetupScore.sln ./
COPY src/JevSetupScore.Core/ src/JevSetupScore.Core/
COPY src/JevSetupScore.Api/ src/JevSetupScore.Api/
RUN dotnet publish src/JevSetupScore.Api/JevSetupScore.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
COPY --from=web /src/web/dist/web/browser ./wwwroot
COPY data/cache ./data/cache
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Data__CacheDirectory=/app/data/cache
ENV Data__AllowLiveFetch=true
ENV ConnectionStrings__Default=Data Source=/app/db/app.db
EXPOSE 8080
ENTRYPOINT ["dotnet", "JevSetupScore.Api.dll"]
