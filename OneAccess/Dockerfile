FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY Shared/ Shared/
COPY OneAccess/ OneAccess/
RUN dotnet publish OneAccess/OneAccess.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "OneAccess.dll"]
