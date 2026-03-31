#!/bin/sh
# Build the Docker image for Minimal.Api with the tag minimal.api:latest

# docker build -t minimal.api:latest .
cd Minimal.Api
dotnet publish -c Release /t:PublishContainer -p:ContainerImageTags=1.0.0


