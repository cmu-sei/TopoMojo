#
#multi-stage target: dev
#
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev
ENV ASPNETCORE_ENVIRONMENT=DEVELOPMENT
COPY . /app
WORKDIR /app/src/TopoMojo.Api
RUN dotnet publish -c Release -o /app/dist
CMD ["dotnet", "run"]

#
#multi-stage target: prod
#
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS prod
ARG commit
ENV COMMIT=$commit
ENV DOTNET_HOSTBUILDER__RELOADCONFIGCHANGE=false
COPY --from=dev /app/dist /app
COPY --from=dev /app/LICENSE.md /app/LICENSE.md
WORKDIR /app
USER $APP_UID
CMD [ "dotnet", "TopoMojo.Api.dll" ]
