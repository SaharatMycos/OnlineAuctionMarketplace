# syntax=docker/dockerfile:1
# One image recipe for all three .NET processes. Pick the host with --build-arg PROJECT:
#   Marketplace.Api | Marketplace.Realtime | Marketplace.Worker
#
# Visual Studio (docker-compose.dcproj, Debug) builds only the `base` stage and mounts the project's
# build output into /app ("fast mode"). `docker compose up --build` builds the full `runtime` image.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
# sharing=locked: compose builds the three images in parallel and they share this cache.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "src/${PROJECT}/${PROJECT}.csproj" -c Release -o /app -p:UseAppHost=false

FROM base AS runtime
ARG PROJECT
ENV APP_DLL=${PROJECT}.dll
COPY --from=build /app .
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
