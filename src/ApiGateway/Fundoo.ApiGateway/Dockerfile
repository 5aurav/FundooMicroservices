FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY FundooMicroservice.slnx ./
COPY src/Shared/Fundoo.Common/Fundoo.Common.csproj src/Shared/Fundoo.Common/
COPY src/ApiGateway/Fundoo.ApiGateway/Fundoo.ApiGateway.csproj src/ApiGateway/Fundoo.ApiGateway/

RUN dotnet restore src/ApiGateway/Fundoo.ApiGateway/Fundoo.ApiGateway.csproj

COPY src/Shared/Fundoo.Common/ src/Shared/Fundoo.Common/
COPY src/ApiGateway/Fundoo.ApiGateway/ src/ApiGateway/Fundoo.ApiGateway/

WORKDIR /app/src/ApiGateway/Fundoo.ApiGateway
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Fundoo.ApiGateway.dll"]
