FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY src/Core/Ditak.Attendance.Core.csproj src/Core/
COPY tests/Core.Tests/Ditak.Attendance.Core.Tests.csproj tests/Core.Tests/
RUN dotnet restore tests/Core.Tests/Ditak.Attendance.Core.Tests.csproj
COPY src/Core/ src/Core/
COPY tests/Core.Tests/ tests/Core.Tests/
RUN dotnet test tests/Core.Tests/Ditak.Attendance.Core.Tests.csproj -c Release --no-restore
