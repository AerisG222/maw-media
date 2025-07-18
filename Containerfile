FROM mcr.microsoft.com/dotnet/sdk:9.0-azurelinux3.0 AS build
WORKDIR /maw-media

# restore
COPY maw-media.sln .
COPY nuget.config .
COPY src/MawMedia/MawMedia.csproj src/MawMedia/
COPY src/MawMedia.Authorization/MawMedia.Authorization.csproj src/MawMedia.Authorization/
COPY src/MawMedia.Models/MawMedia.Models.csproj src/MawMedia.Models/
COPY src/MawMedia.Services/MawMedia.Services.csproj src/MawMedia.Services/
COPY src/MawMedia.Services.Abstractions/MawMedia.Services.Abstractions.csproj src/MawMedia.Services.Abstractions/
RUN dotnet restore --runtime linux-x64 src/MawMedia/MawMedia.csproj

# build
COPY src/. src/
RUN dotnet publish \
        --no-restore \
        --no-self-contained \
        -c Release \
        -r linux-x64 \
        -o /build \
        src/MawMedia/MawMedia.csproj


# build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-azurelinux3.0-distroless-extra
WORKDIR /maw-media

COPY --from=build /build .

EXPOSE 8081

ENTRYPOINT [ "/maw-media/MawMedia" ]
