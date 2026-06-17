FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY Backend/OnlineDungeonServer/publish/ .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5086 5087/udp 5088
ENTRYPOINT ["dotnet", "OnlineDungeonServer.dll"]
