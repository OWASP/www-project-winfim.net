#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["WinFIM.NET.Service/WinFIM.NET.Service.csproj", "WinFIM.NET.Service/"]

# Restores the dependencies and tools of the project (e.g. Nuget packages)
RUN dotnet restore "WinFIM.NET.Service/WinFIM.NET.Service.csproj"
# Copies all files and directories (recursively) from the repo's root dir
# Note: If you get docker build errors at "Determining projects to restore", the fix is to launch Docker Desktop and add DNS entries in the settings per https://github.com/dotnet/core/issues/8048

COPY . .
WORKDIR "/src/WinFIM.NET.Service"
RUN dotnet build "WinFIM.NET.Service.csproj" -c Release -o /app/build
ENTRYPOINT ["cmd"]

FROM build AS publish
RUN dotnet publish "WinFIM.NET.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# copy files from the "publish" docker stage in the "c:\app\publish" directory to the "c:\app" directory

# ENTRYPOINT ["dotnet", "WinFIM.NET.Service.dll"]
ENTRYPOINT ["cmd"]