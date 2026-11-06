# syntax=docker/dockerfile:1.7
# One Dockerfile for both services:  docker build --build-arg PROJECT=LockGuard.Api .
#                                    docker build --build-arg PROJECT=LockGuard.Detector .
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG PROJECT=LockGuard.Api
WORKDIR /src

# Restore first (layer cached until a project file changes).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/LockGuard.Domain/LockGuard.Domain.csproj src/LockGuard.Domain/
COPY src/LockGuard.Application/LockGuard.Application.csproj src/LockGuard.Application/
COPY src/LockGuard.Infrastructure/LockGuard.Infrastructure.csproj src/LockGuard.Infrastructure/
COPY src/LockGuard.Api/LockGuard.Api.csproj src/LockGuard.Api/
COPY src/LockGuard.Detector/LockGuard.Detector.csproj src/LockGuard.Detector/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj

COPY src/ src/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG PROJECT=LockGuard.Api
ENV APP_DLL=${PROJECT}.dll \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
