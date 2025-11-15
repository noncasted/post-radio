FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 9120

ENV ASPNETCORE_URLS=http://+:9120

ENV ConnectionStrings__postgres=${DB_CONNECTION_STRING}
ENV ConnectionStrings__otel=${OTEL_CONNECTION_STRING}
ENV SERVICE_NAME=Silo
ENV SERVICE_PUBLIC_URL=${SERVICE_PUBLIC_URL}


FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY backend/ .
COPY shared/ .

RUN dotnet restore "Infrastructure/Orleans/Silo/Silo.csproj"

COPY . .
WORKDIR "/src/backend/Infrastructure/Orleans/Silo"

RUN dotnet build "Silo.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Silo.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Infrastructure.Orleans.Silo.dll"]
