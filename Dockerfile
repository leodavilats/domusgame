# Imagem única: o front-end compilado vira conteúdo estático servido pela própria API.
# Um deploy, um container, uma origem (doc 05, secao 2).

FROM node:22-alpine AS frontend
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci
COPY frontend/ ./
RUN npm run build -- --outDir dist --emptyOutDir

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/Directory.Build.props ./
COPY backend/src/ ./src/
RUN dotnet restore src/Domus.Api/Domus.Api.csproj
COPY --from=frontend /app/dist ./src/Domus.Api/wwwroot
RUN dotnet publish src/Domus.Api/Domus.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Domus.Api.dll"]
