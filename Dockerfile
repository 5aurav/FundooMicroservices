# ==============================================================================
# Fundoo Microservices — Universal Multi-Stage Dockerfile
# ==============================================================================

# Stage 1: Build & Publish using .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT_PATH
WORKDIR /src

# Copy all source files so project references (e.g. Fundoo.Common) resolve
COPY . .

# Publish the specified project into /app/publish
RUN dotnet publish ${PROJECT_PATH} -c Release -o /app/publish

# Stage 2: Runtime using lightweight ASP.NET Core 8 image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# Automatically execute the assembly compiled for this project
CMD ["sh", "-c", "dotnet $(ls *.runtimeconfig.json | head -n 1 | sed 's/.runtimeconfig.json/.dll/')"]
