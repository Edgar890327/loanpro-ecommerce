FROM node:24-alpine AS frontend-build
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY . .
COPY --from=frontend-build /src/web/dist ./src/Store.Api/wwwroot
RUN dotnet publish src/Store.Api/Store.Api.csproj --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=api-build /app/publish .
ENTRYPOINT ["dotnet", "Store.Api.dll"]
