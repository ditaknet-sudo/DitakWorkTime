FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DitakAttendance.sln ./
COPY src/Core/Ditak.Attendance.Core.csproj src/Core/
COPY src/Api/Ditak.Attendance.Api.csproj src/Api/
COPY src/Worker/Ditak.Attendance.Worker.csproj src/Worker/
RUN dotnet restore src/Api/Ditak.Attendance.Api.csproj
COPY src/Core/ src/Core/
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Ditak.Attendance.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID:$APP_UID /home/app/.aspnet
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ditak.Attendance.Api.dll"]
