# Community profile service deployment

This repo ships an ASP.NET Core minimal API at `src/MlxPep.Service` that serves community profile metadata and persists profile payloads in Azure Blob Storage.

## Container build

```bash
docker build -t mlxpep-service -f src/MlxPep.Service/Dockerfile .
```

Run the container with a Blob Storage connection string and the desired public port:

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__AzureBlobStorage="<azure-blob-connection-string>" \
  -e RateLimit__DefaultLimit="100" \
  -e RateLimit__WindowSizeSeconds="60" \
  mlxpep-service
```

The default listener uses `ASPNETCORE_URLS=http://+:8080` inside the container, so the service is reachable from the host on port 8080.

## Azure Blob Storage configuration

The service reads its Blob Storage account from `ConnectionStrings:AzureBlobStorage` at runtime. In Docker or App Service, set the value via `ConnectionStrings__AzureBlobStorage` or the platform's equivalent secret configuration.

The profile payloads are stored in a container named `profiles` in the configured storage account. The service expects a Blob Storage account with the `profiles` container already created.

## Public download URL guidance

For a published profile, the canonical public URL is:

```text
https://<storage-account>.blob.core.windows.net/profiles/<profile-id>.json
```

If you use a custom domain or CDN, the same pattern applies with the custom hostname as the origin base.

For production workloads, prefer a container platform such as Azure Container Apps or Azure App Service in front of the service, and configure Blob container anonymous read access only if your distribution model intentionally allows public profile downloads.

## Health check

The service exposes:

```text
GET /health
```

Expected response:

```json
{"status":"healthy"}
```

## Notes

- The service is configured for single-file publish and is intended to run as a self-contained Linux executable in container-hosted environments.
- Rate limiting defaults are defined in `src/MlxPep.Service/appsettings.json` and may be overridden via environment variables such as `RateLimit__DefaultLimit` and `RateLimit__WindowSizeSeconds`.
