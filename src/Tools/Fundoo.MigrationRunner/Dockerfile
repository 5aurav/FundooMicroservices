FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Fundoo.Common/Fundoo.Common.csproj src/Shared/Fundoo.Common/
COPY src/Services/User/Fundoo.User.Domain/Fundoo.User.Domain.csproj src/Services/User/Fundoo.User.Domain/
COPY src/Services/User/Fundoo.User.Application/Fundoo.User.Application.csproj src/Services/User/Fundoo.User.Application/
COPY src/Services/User/Fundoo.User.Infrastructure/Fundoo.User.Infrastructure.csproj src/Services/User/Fundoo.User.Infrastructure/
COPY src/Services/Notification/Fundoo.Notification.Domain/Fundoo.Notification.Domain.csproj src/Services/Notification/Fundoo.Notification.Domain/
COPY src/Services/Notification/Fundoo.Notification.Application/Fundoo.Notification.Application.csproj src/Services/Notification/Fundoo.Notification.Application/
COPY src/Services/Notification/Fundoo.Notification.Infrastructure/Fundoo.Notification.Infrastructure.csproj src/Services/Notification/Fundoo.Notification.Infrastructure/
COPY src/Tools/Fundoo.MigrationRunner/Fundoo.MigrationRunner.csproj src/Tools/Fundoo.MigrationRunner/

RUN dotnet restore src/Tools/Fundoo.MigrationRunner/Fundoo.MigrationRunner.csproj

COPY src/Shared/Fundoo.Common/ src/Shared/Fundoo.Common/
COPY src/Services/User/ src/Services/User/
COPY src/Services/Notification/ src/Services/Notification/
COPY src/Tools/Fundoo.MigrationRunner/ src/Tools/Fundoo.MigrationRunner/

WORKDIR /app/src/Tools/Fundoo.MigrationRunner
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "Fundoo.MigrationRunner.dll"]
