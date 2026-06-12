# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Support.Domain/Support.Domain.csproj src/Support.Domain/
COPY src/Support.Application/Support.Application.csproj src/Support.Application/
COPY src/Support.Infrastructure/Support.Infrastructure.csproj src/Support.Infrastructure/
COPY src/Support.Api/Support.Api.csproj src/Support.Api/
RUN dotnet restore src/Support.Api/Support.Api.csproj

COPY src/ src/
RUN dotnet publish src/Support.Api/Support.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Support.Api.dll"]
