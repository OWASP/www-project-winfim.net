#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS final
WORKDIR /src
# COPY ["WinFIM.NET.Service/WinFIM.NET.Service.csproj", "WinFIM.NET.Service/"]
COPY . .

WORKDIR "/src/WinFIM.NET.Service"
# re-add the SQLite nuget package so the DLL's are copied to the directory correctly
# RUN dotnet add package System.Data.SQLite.Core

# COPY ["WinFIM.NET.Service/", "WinFIM.NET.Service/"]
RUN dotnet restore "WinFIM.NET.Service.csproj"

WORKDIR "/src/WinFIM.NET.Service"
RUN dotnet build "WinFIM.NET.Service.csproj" -c Release -o /app/build

ENTRYPOINT ["cmd"]

#FROM build AS publish
#RUN dotnet publish "WinFIM.NET.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false
#
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "WinFIM.NET.Service.dll"]
#