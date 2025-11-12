FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderFlow.BankMock/OrderFlow.BankMock.csproj", "OrderFlow.BankMock/"]
RUN dotnet restore "OrderFlow.BankMock/OrderFlow.BankMock.csproj"
COPY . .
WORKDIR "/src/OrderFlow.BankMock"
RUN dotnet build "OrderFlow.BankMock.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.BankMock.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.BankMock.dll"]

