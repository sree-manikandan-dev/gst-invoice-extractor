# Single-container image for free hosting (e.g. Render): the .NET API also serves the Angular app.
# docker-compose.yml still uses the separate backend/ and frontend/ images for local work.

FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npx ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY backend/GstInvoice.sln ./
COPY backend/src/GstInvoice.Core/GstInvoice.Core.csproj src/GstInvoice.Core/
COPY backend/src/GstInvoice.Api/GstInvoice.Api.csproj src/GstInvoice.Api/
RUN dotnet restore src/GstInvoice.Api/GstInvoice.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/GstInvoice.Api/GstInvoice.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist/frontend/browser ./wwwroot
# Render (and most hosts) pass the port in $PORT; default to 8080 elsewhere.
ENV PORT=8080
EXPOSE 8080
USER app
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} exec dotnet GstInvoice.Api.dll"]
