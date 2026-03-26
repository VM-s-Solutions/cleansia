FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Cleansia.Functions/Cleansia.Functions.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0

# Install Chromium and dependencies required by PuppeteerSharp.
# Try 'chromium', fall back to 'chromium-browser' (varies by Debian/Ubuntu version).
RUN apt-get update \
    && (apt-get install -y --no-install-recommends chromium fonts-liberation \
        || apt-get install -y --no-install-recommends chromium-browser fonts-liberation) \
    && rm -rf /var/lib/apt/lists/*

# Detect the installed chromium binary path
RUN CHROMIUM=$(which chromium 2>/dev/null || which chromium-browser 2>/dev/null) \
    && echo "Chromium binary: ${CHROMIUM}" \
    && ln -sf "${CHROMIUM}" /usr/local/bin/chromium-stable

ENV PUPPETEER_EXECUTABLE_PATH=/usr/local/bin/chromium-stable

COPY --from=build /app/publish /home/site/wwwroot
