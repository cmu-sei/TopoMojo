#
#multi-stage target: dev
#
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ARG VERSION
ENV ASPNETCORE_ENVIRONMENT=DEVELOPMENT
COPY . /home/app
WORKDIR /home/app/src/TopoMojo.Api
RUN dotnet publish --use-current-runtime -o /home/app/dist /p:Version=${VERSION:-1.0.0} /p:AssemblyVersion=${VERSION:-1.0.0}
CMD ["dotnet", "run"]

#
#multi-stage target: prod
#
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS prod
ARG commit
ENV COMMIT=$commit
ENV DOTNET_HOSTBUILDER__RELOADCONFIGCHANGE=false
WORKDIR /home/app
COPY --from=dev /home/app/dist .
COPY --from=dev /home/app/LICENSE.md LICENSE.md
USER $APP_UID
CMD [ "dotnet", "TopoMojo.Api.dll" ]
