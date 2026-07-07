# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/ExternalNode/ExternalNode.csproj -c Release -o /app

# Runtime: ASP.NET + docker CLI + a local daemon (run --privileged).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends docker.io ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    NodeAgent__DataRoot=/var/app/data
EXPOSE 8080
# Starts a local Docker daemon then the agent. Requires `--privileged`.
ENTRYPOINT ["/bin/sh", "-c", "dockerd >/var/log/dockerd.log 2>&1 & exec dotnet ExternalNode.dll"]
