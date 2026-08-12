FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy central configuration files
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Copy project files for restore
COPY src/MlxPep.Core/MlxPep.Core.csproj src/MlxPep.Core/
COPY src/MlxPep.Service/MlxPep.Service.csproj src/MlxPep.Service/
RUN dotnet restore "src/MlxPep.Service/MlxPep.Service.csproj"

COPY . .
WORKDIR "/src/src/MlxPep.Service"
RUN dotnet publish "MlxPep.Service.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["./MlxPep.Service"]
