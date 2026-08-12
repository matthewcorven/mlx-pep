# mlx-pep

Hardware-aware Apple Silicon config matrix generator for local Ornith MTPLX use.

## Usage

```bash
python3 generate_ornith_matrix.py
python3 generate_ornith_matrix.py --write current_matrix.md
python3 generate_ornith_matrix.py --json
```

The script is read-only. It does not unload oMLX models, modify load state, or uninstall anything.

## Community service deployment

The repo includes a minimal ASP.NET Core profile service at `src/MlxPep.Service`.

```bash
docker build -t mlxpep-service -f src/MlxPep.Service/Dockerfile .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__AzureBlobStorage="<connection-string>" \
  mlxpep-service
```

For additional setup guidance, see `docs/service-deployment.md`.
