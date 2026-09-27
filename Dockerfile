# Etapa 1: compilar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PlataformaCursos.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: ejecutar
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=America/Lima
EXPOSE 8080
# Render indica el puerto en la variable PORT
CMD ["sh", "-c", "export ASPNETCORE_HTTP_PORTS=${PORT:-8080} && exec dotnet PlataformaCursos.dll"]