FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY /aspnet-core-dotnet-core/*.csproj ./
RUN dotnet restore

# Copy everything else and build  
COPY /aspnet-core-dotnet-core/ ./
RUN dotnet publish -c Release -o out 

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:5.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "aspnet-core-dotnet-core.dll"]
ENV ASPNETCORE_URLS=http://+:80  
EXPOSE 80

