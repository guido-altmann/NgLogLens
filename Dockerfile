# NgLogLens als statische Seite hinter nginx (SPEC 9). Mehrstufig: das SDK baut,
# ausgeliefert wird nur publish/wwwroot mit nginx.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Erst die Projektdateien, damit das Restore bei reinen Codeänderungen im Cache bleibt.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/LogLens.Core/LogLens.Core.csproj src/LogLens.Core/
COPY src/LogLens.Web/LogLens.Web.csproj src/LogLens.Web/
RUN dotnet restore src/LogLens.Web/LogLens.Web.csproj

COPY src/ src/
RUN dotnet publish src/LogLens.Web/LogLens.Web.csproj -c Release -o /app/publish

FROM nginx:stable-alpine AS runtime
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY deploy/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
