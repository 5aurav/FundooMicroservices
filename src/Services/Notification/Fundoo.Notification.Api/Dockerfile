FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Fundoo.Common/Fundoo.Common.csproj src/Shared/Fundoo.Common/
COPY src/Services/Notification/Fundoo.Notification.Domain/Fundoo.Notification.Domain.csproj src/Services/Notification/Fundoo.Notification.Domain/
COPY src/Services/Notification/Fundoo.Notification.Application/Fundoo.Notification.Application.csproj src/Services/Notification/Fundoo.Notification.Application/
COPY src/Services/Notification/Fundoo.Notification.Infrastructure/Fundoo.Notification.Infrastructure.csproj src/Services/Notification/Fundoo.Notification.Infrastructure/
COPY src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api.csproj src/Services/Notification/Fundoo.Notification.Api/

RUN dotnet restore src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api.csproj

COPY src/Shared/Fundoo.Common/ src/Shared/Fundoo.Common/
COPY src/Services/Notification/ src/Services/Notification/

WORKDIR /app/src/Services/Notification/Fundoo.Notification.Api
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Fundoo.Notification.Api.dll"]
