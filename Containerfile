FROM mcr.microsoft.com/dotnet/sdk:9.0-azurelinux3.0 AS build
WORKDIR /maw-api

# restore
COPY maw-api.sln   .
COPY nuget.config  .
COPY src/MawWww/MawApi.csproj                 src/MawApi/
RUN dotnet restore --runtime linux-x64

# build
COPY src/. src/
RUN dotnet publish \
        --no-restore \
        --no-self-contained \
        -c Release \
        -r linux-x64 \
        -o /build \
        src/MawApi/MawApi.csproj


# build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-azurelinux3.0-distroless-extra
WORKDIR /maw-api

COPY --from=build /build .

EXPOSE 5000

ENTRYPOINT [ "/maw-api/MawApi" ]
