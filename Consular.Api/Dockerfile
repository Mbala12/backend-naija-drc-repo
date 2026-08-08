FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Consular.Shared/Consular.Shared.csproj Consular.Shared/
COPY Consular.Api/Consular.Api.csproj Consular.Api/
RUN dotnet restore Consular.Api/Consular.Api.csproj
COPY Consular.Shared/ Consular.Shared/
COPY Consular.Api/ Consular.Api/
RUN dotnet publish Consular.Api/Consular.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# PdfSharp (ReportExportWriter's PDF export) needs real font files — this image ships none by
# default. fonts-dejavu-core is small and MIT/Bitstream-Vera-licensed.
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/uploads && chown -R app:app /app/uploads
# Run as non-root - the default aspnet image ships an "app" user for exactly this.
USER app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Consular.Api.dll"]
