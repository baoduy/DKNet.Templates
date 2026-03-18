# 1. Build the .nupkg
cd src
dotnet pack DKNet.SlimBus.Template.csproj -c Release -o ./nupkgs

# 2. Test locally
#dotnet new install ./nupkgs/DKNet.SlimBus.Template.1.0.0.nupkg
#dotnet new dknet-slimbus -n Acme.OrderService

# 3. Push to nuget.org
# dotnet nuget push ./nupkgs/DKNet.SlimBus.Template.1.0.0.nupkg \
#   --api-key <YOUR_NUGET_API_KEY> \
#   --source https://api.nuget.org/v3/index.json