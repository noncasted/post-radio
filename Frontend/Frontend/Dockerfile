FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

ENV MINIO_ENDPOINT=$MINIO_ENDPOINT
ENV MINIO_ACCESSKEY=$MINIO_ACCESSKEY
ENV MINIO_SECRETKEY=$MINIO_SECRETKEY

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Frontend/Frontend/Frontend.csproj", "Frontend/Frontend/"]
RUN dotnet restore "Frontend/Frontend/Frontend.csproj"
COPY . .
WORKDIR "/src/Frontend/Frontend"
RUN dotnet build "Frontend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Frontend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Frontend.dll"]
