FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Fundoo.Common/Fundoo.Common.csproj src/Shared/Fundoo.Common/
COPY src/Services/Notes/Fundoo.Notes.Domain/Fundoo.Notes.Domain.csproj src/Services/Notes/Fundoo.Notes.Domain/
COPY src/Services/Notes/Fundoo.Notes.Application/Fundoo.Notes.Application.csproj src/Services/Notes/Fundoo.Notes.Application/
COPY src/Services/Notes/Fundoo.Notes.Infrastructure/Fundoo.Notes.Infrastructure.csproj src/Services/Notes/Fundoo.Notes.Infrastructure/
COPY src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api.csproj src/Services/Notes/Fundoo.Notes.Api/

RUN dotnet restore src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api.csproj

COPY src/Shared/Fundoo.Common/ src/Shared/Fundoo.Common/
COPY src/Services/Notes/ src/Services/Notes/

WORKDIR /app/src/Services/Notes/Fundoo.Notes.Api
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Fundoo.Notes.Api.dll"]
