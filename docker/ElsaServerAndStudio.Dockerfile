﻿FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /source

# copy sources.
COPY src/. ./src
COPY ./NuGet.Config ./
COPY *.props ./

# restore packages.
RUN dotnet restore "./src/bundles/ElsaStudioWebAssembly/ElsaStudioWebAssembly.csproj"
RUN dotnet restore "./src/bundles/Elsa.ServerAndStudio.Web/Elsa.ServerAndStudio.Web.csproj"

# build and publish (UseAppHost=false creates platform independent binaries).
WORKDIR /source/src/bundles/Elsa.AllInOne.Web
RUN dotnet build "Elsa.ServerAndStudio.Web.csproj" -c Release -o /app/build
RUN dotnet publish "Elsa.ServerAndStudio.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore -f net8.0

# move binaries into smaller base image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 80/tcp
EXPOSE 443/tcp
ENTRYPOINT ["dotnet", "Elsa.ServerAndStudio.Web.dll"]