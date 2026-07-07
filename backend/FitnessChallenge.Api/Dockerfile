# syntax=docker/dockerfile:1
# Build context is ./backend

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore against the project graph first for better layer caching.
COPY FitnessChallenge.Domain/*.csproj FitnessChallenge.Domain/
COPY FitnessChallenge.Application/*.csproj FitnessChallenge.Application/
COPY FitnessChallenge.Infrastructure/*.csproj FitnessChallenge.Infrastructure/
COPY FitnessChallenge.Api/*.csproj FitnessChallenge.Api/
RUN dotnet restore FitnessChallenge.Api/FitnessChallenge.Api.csproj

COPY . .
RUN dotnet publish FitnessChallenge.Api/FitnessChallenge.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /data

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Default="Data Source=/data/fitness-challenge.db"

EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "FitnessChallenge.Api.dll"]
