# Build the Docker image
docker build -f ".\WinFIM.NET.Service\Dockerfile" --force-rm -t winfimnetservice  --label "com.microsoft.visual-studio.project-name=WinFIM.NET.Service" .

docker run --name winfim --volume "C:\:C:\host:ro" --rm -it winfimnetservice:latest cmd