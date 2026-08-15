FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY CheckoutService.Demo.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "CheckoutService.Demo.dll"]
