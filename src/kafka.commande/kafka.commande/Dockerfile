# base
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

# build
FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY kafka.commande/kafka.commande.csproj kafka.commande/
RUN dotnet restore "kafka.commande/kafka.commande.csproj"
COPY . .
WORKDIR "/src/kafka.commande"
RUN dotnet build "kafka.commande.csproj" -c Release -o /app/build

# tests
FROM build AS tests
WORKDIR /src
COPY kafka.commande.tests/kafka.commande.tests.csproj kafka.commande.tests/
WORKDIR "/src/kafka.commande.tests"
RUN dotnet restore "kafka.commande.tests.csproj"
RUN /bin/bash -c "dotnet test kafka.commande.tests.csproj -c Release --filter Category=unit --verbosity n --logger \"trx;LogFileName=test.trx\"" 

# publish
FROM build AS publish
RUN dotnet publish "kafka.commande.csproj" -c Release -o /app/publish

# final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "kafka.commande.dll"]
