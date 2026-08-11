# mlx-pep

Hardware-aware Apple Silicon config matrix generator for local Ornith MTPLX use.

## Usage

```bash
python3 generate_ornith_matrix.py
python3 generate_ornith_matrix.py --write current_matrix.md
python3 generate_ornith_matrix.py --json
```

The script is read-only. It does not unload oMLX models, modify load state, or uninstall anything.
