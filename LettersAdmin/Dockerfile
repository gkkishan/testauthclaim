FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY Shared/ Shared/
COPY LettersAdmin/ LettersAdmin/
RUN dotnet publish LettersAdmin/LettersAdmin.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "LettersAdmin.dll"]
