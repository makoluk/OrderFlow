FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.OrderService/OrderFlow.OrderService.csproj", "OrderFlow.OrderService/"]
COPY ["OrderFlow.Shared.Contracts/OrderFlow.Shared.Contracts.csproj", "OrderFlow.Shared.Contracts/"]
RUN dotnet restore "OrderFlow.OrderService/OrderFlow.OrderService.csproj"
COPY . .
WORKDIR "/src/OrderFlow.OrderService"
RUN dotnet build "OrderFlow.OrderService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.OrderService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.OrderService.dll"]

