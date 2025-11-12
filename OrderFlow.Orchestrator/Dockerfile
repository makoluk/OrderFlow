FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.Orchestrator/OrderFlow.Orchestrator.csproj", "OrderFlow.Orchestrator/"]
COPY ["OrderFlow.Shared.Contracts/OrderFlow.Shared.Contracts.csproj", "OrderFlow.Shared.Contracts/"]
RUN dotnet restore "OrderFlow.Orchestrator/OrderFlow.Orchestrator.csproj"
COPY . .
WORKDIR "/src/OrderFlow.Orchestrator"
RUN dotnet build "OrderFlow.Orchestrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.Orchestrator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Orchestrator.dll"]

