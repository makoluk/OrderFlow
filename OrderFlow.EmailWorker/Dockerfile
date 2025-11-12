FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.EmailWorker/OrderFlow.EmailWorker.csproj", "OrderFlow.EmailWorker/"]
COPY ["OrderFlow.Shared.Contracts/OrderFlow.Shared.Contracts.csproj", "OrderFlow.Shared.Contracts/"]
RUN dotnet restore "OrderFlow.EmailWorker/OrderFlow.EmailWorker.csproj"
COPY . .
WORKDIR "/src/OrderFlow.EmailWorker"
RUN dotnet build "OrderFlow.EmailWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.EmailWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.EmailWorker.dll"]

