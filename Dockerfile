# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Otakarr.csproj", "./"]
RUN dotnet restore "Otakarr.csproj"

# Copy the rest of the source code and publish
COPY . .
RUN dotnet publish "Otakarr.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set default port to 8000
ENV PORT=8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "Otakarr.dll"]
