FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Api.csproj
RUN dotnet publish Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
# ASP.NET watches appsettings*.json for hot reload by default, which costs an
# inotify instance per file. Render's free instances share a host-wide limit of
# 128, so when neighbours have consumed it the watcher throws inside
# WebApplication.CreateBuilder — before any of this application's own code runs,
# and intermittently, since it depends on what else is on the box. Nothing here
# rewrites its configuration at runtime; the platform restarts the container on
# an environment change anyway.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
# The port is read at start, not baked in: Render injects PORT (10000 by
# default) and expects the process to bind it, while Railway and a local
# `docker run` leave it unset. An ENV line cannot do this — it is expanded when
# the image is built, not when the container starts — so the shell form is what
# makes the same image work on either host. 0.0.0.0 rather than localhost is
# required for the platform load balancer to reach it.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet Api.dll"]
