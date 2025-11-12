FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.StockService/OrderFlow.StockService.csproj", "OrderFlow.StockService/"]
COPY ["OrderFlow.Shared.Contracts/OrderFlow.Shared.Contracts.csproj", "OrderFlow.Shared.Contracts/"]
RUN dotnet restore "OrderFlow.StockService/OrderFlow.StockService.csproj"
COPY . .
WORKDIR "/src/OrderFlow.StockService"
RUN dotnet build "OrderFlow.StockService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.StockService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.StockService.dll"]

