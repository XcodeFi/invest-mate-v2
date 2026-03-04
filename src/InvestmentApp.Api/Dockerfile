# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["InvestmentApp.Api.csproj", "./"]
COPY ["../InvestmentApp.Application/InvestmentApp.Application.csproj", "../InvestmentApp.Application/"]
COPY ["../InvestmentApp.Domain/InvestmentApp.Domain.csproj", "../InvestmentApp.Domain/"]
COPY ["../InvestmentApp.Infrastructure/InvestmentApp.Infrastructure.csproj", "../InvestmentApp.Infrastructure/"]

RUN dotnet restore "InvestmentApp.Api.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "InvestmentApp.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "InvestmentApp.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InvestmentApp.Api.dll"]