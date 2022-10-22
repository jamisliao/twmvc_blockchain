FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["twmvc_example/twmvc_example.csproj", "twmvc_example/"]
RUN dotnet restore "twmvc_example/twmvc_example.csproj"
COPY . .
WORKDIR "/src/twmvc_example"
RUN dotnet build "twmvc_example.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "twmvc_example.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "twmvc_example.dll"]
