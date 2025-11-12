FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.PaymentService/OrderFlow.PaymentService.csproj", "OrderFlow.PaymentService/"]
COPY ["OrderFlow.Shared.Contracts/OrderFlow.Shared.Contracts.csproj", "OrderFlow.Shared.Contracts/"]
RUN dotnet restore "OrderFlow.PaymentService/OrderFlow.PaymentService.csproj"
COPY . .
WORKDIR "/src/OrderFlow.PaymentService"
RUN dotnet build "OrderFlow.PaymentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.PaymentService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.PaymentService.dll"]

