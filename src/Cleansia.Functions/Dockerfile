FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
# Build from the repository root so the linked email resources are available.
COPY src/ ./src/
COPY email-templates/ ./email-templates/
RUN dotnet publish src/Cleansia.Functions/Cleansia.Functions.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
COPY --from=build /app/publish /home/site/wwwroot
