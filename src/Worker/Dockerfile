FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DitakAttendance.sln ./
COPY src/Core/Ditak.Attendance.Core.csproj src/Core/
COPY src/Api/Ditak.Attendance.Api.csproj src/Api/
COPY src/Worker/Ditak.Attendance.Worker.csproj src/Worker/
RUN dotnet restore src/Worker/Ditak.Attendance.Worker.csproj
COPY src/Core/ src/Core/
COPY src/Worker/ src/Worker/
RUN dotnet publish src/Worker/Ditak.Attendance.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
USER $APP_UID
ENTRYPOINT ["dotnet", "Ditak.Attendance.Worker.dll"]
