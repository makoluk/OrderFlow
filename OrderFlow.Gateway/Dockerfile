FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.Gateway/OrderFlow.Gateway.csproj", "OrderFlow.Gateway/"]
RUN dotnet restore "OrderFlow.Gateway/OrderFlow.Gateway.csproj"
COPY . .
WORKDIR "/src/OrderFlow.Gateway"
RUN dotnet build "OrderFlow.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Gateway.dll"]

