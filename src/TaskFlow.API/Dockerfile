FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to maximize Docker layer caching
COPY src/TaskFlow.Domain/TaskFlow.Domain.csproj src/TaskFlow.Domain/
COPY src/TaskFlow.Application/TaskFlow.Application.csproj src/TaskFlow.Application/
COPY src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj src/TaskFlow.Infrastructure/
COPY src/TaskFlow.API/TaskFlow.API.csproj src/TaskFlow.API/
RUN dotnet restore src/TaskFlow.API/TaskFlow.API.csproj

# Copy source and publish
COPY src/TaskFlow.Domain/ src/TaskFlow.Domain/
COPY src/TaskFlow.Application/ src/TaskFlow.Application/
COPY src/TaskFlow.Infrastructure/ src/TaskFlow.Infrastructure/
COPY src/TaskFlow.API/ src/TaskFlow.API/
RUN dotnet publish src/TaskFlow.API/TaskFlow.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskFlow.API.dll"]
