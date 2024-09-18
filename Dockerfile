#
#multi-stage target: dev
#
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev
ENV ASPNETCORE_ENVIRONMENT=DEVELOPMENT
COPY . /home/app
WORKDIR /home/app/src/TopoMojo.Api
RUN dotnet publish -c Release -o /app/dist
CMD ["dotnet", "run"]

#
#multi-stage target: prod
#
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS prod
ARG commit
ENV COMMIT=$commit
ENV DOTNET_HOSTBUILDER__RELOADCONFIGCHANGE=false
WORKDIR /home/app
COPY --from=dev /home/app/dist .
COPY --from=dev /home/app/LICENSE.md LICENSE.md
USER $APP_UID
CMD [ "dotnet", "TopoMojo.Api.dll" ]
