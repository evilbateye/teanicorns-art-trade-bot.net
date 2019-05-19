FROM microsoft/dotnet:2.2-sdk AS build-env

WORKDIR /app
COPY teanicorns-art-trade-bot/ ./teanicorns-art-trade-bot

RUN dotnet publish ./teanicorns-art-trade-bot/teanicorns-art-trade-bot.csproj -c Release -r debian-x64 -o ./Build
# RUN dotnet publish ./teanicorns-art-trade-bot/teanicorns-art-trade-bot.csproj -c Release -o /app/Build

FROM ubuntu:bionic
WORKDIR /app
COPY --from=build-env /app/teanicorns-art-trade-bot/Build ./

RUN apt-get update && apt-get install curl liblttng-ust0 libssl1.0.0 libkrb5-3 zlib1g libicu60 libgdiplus libunwind8 icu-devtools tar -y

RUN curl -L --http1.1 http://download.icu-project.org/files/icu4c/62.1/icu4c-62_1-Ubuntu-18.04-x64.tgz --output icu.tgz \
    && tar -xf icu.tgz -C / \
    && export LD_LIBRARY_PATH=/usr/local/lib \
    && rm icu.tgz

# RUN apt-get install libgdiplus
# Uncomment the above line if your teanicorns-art-trade-bot installation doesn't support libgdiplus.

# RUN apt-get -qq update && apt-get install -y libfontconfig1
# ENTRYPOINT [ "dotnet", "teanicorns-art-trade-bot.dll" ]
ENTRYPOINT [ "./teanicorns-art-trade-bot" ]


