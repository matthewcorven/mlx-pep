# Project Context

- **Project:** mlx-pep
- **Created:** 2026-08-11
- **Requested by:** @matthewcorven
- **Product:** Apple Silicon local-model profile toolkit for discovering, applying, generating, and sharing tuned JSONL profiles
- **Stack:** .NET 10, System.CommandLine, Terminal.Gui, ASP.NET Core minimal API, Azure Blob Storage, Python model-assessor, Hugging Face cache, oMLX

## Core Context

Agent Trinity initialized as CLI/Harness Dev for commands, TUI parity, and local config emission.

## Recent Updates

📌 Team cast on 2026-08-11 using The Matrix names.

## Learnings

The CLI is the source of truth for all operations, and every command should support `--json`.
