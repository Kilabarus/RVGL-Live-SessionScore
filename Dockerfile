FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/RvglLiveScore/RvglLiveScore.csproj src/RvglLiveScore/
RUN dotnet restore src/RvglLiveScore/RvglLiveScore.csproj
COPY src/RvglLiveScore/ src/RvglLiveScore/
RUN dotnet publish src/RvglLiveScore/RvglLiveScore.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RvglLiveScore.dll"]
