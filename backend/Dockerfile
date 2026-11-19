# Multi-stage build: the SDK image compiles, the small runtime image runs.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy project files first so `dotnet restore` is cached when only code changes.
COPY GstInvoice.sln ./
COPY src/GstInvoice.Core/GstInvoice.Core.csproj src/GstInvoice.Core/
COPY src/GstInvoice.Api/GstInvoice.Api.csproj src/GstInvoice.Api/
RUN dotnet restore src/GstInvoice.Api/GstInvoice.Api.csproj
COPY src/ src/
RUN dotnet publish src/GstInvoice.Api/GstInvoice.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Run as the non-root user that ships with the .NET images.
USER app
ENTRYPOINT ["dotnet", "GstInvoice.Api.dll"]
