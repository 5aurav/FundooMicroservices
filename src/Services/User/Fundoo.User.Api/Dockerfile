FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Fundoo.Common/Fundoo.Common.csproj src/Shared/Fundoo.Common/
COPY src/Services/User/Fundoo.User.Domain/Fundoo.User.Domain.csproj src/Services/User/Fundoo.User.Domain/
COPY src/Services/User/Fundoo.User.Application/Fundoo.User.Application.csproj src/Services/User/Fundoo.User.Application/
COPY src/Services/User/Fundoo.User.Infrastructure/Fundoo.User.Infrastructure.csproj src/Services/User/Fundoo.User.Infrastructure/
COPY src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj src/Services/User/Fundoo.User.Api/

RUN dotnet restore src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj

COPY src/Shared/Fundoo.Common/ src/Shared/Fundoo.Common/
COPY src/Services/User/ src/Services/User/

WORKDIR /app/src/Services/User/Fundoo.User.Api
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Fundoo.User.Api.dll"]
