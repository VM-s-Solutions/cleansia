FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Cleansia.Functions/Cleansia.Functions.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
COPY --from=build /app/publish /home/site/wwwroot
