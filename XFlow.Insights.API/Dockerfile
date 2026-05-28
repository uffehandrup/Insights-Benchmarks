FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY XFlow.Insights.API/XFlow.Insights.API.csproj XFlow.Insights.API/
RUN dotnet restore XFlow.Insights.API/XFlow.Insights.API.csproj

COPY XFlow.Insights.API/ XFlow.Insights.API/
RUN dotnet publish XFlow.Insights.API/XFlow.Insights.API.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "XFlow.Insights.API.dll"]
