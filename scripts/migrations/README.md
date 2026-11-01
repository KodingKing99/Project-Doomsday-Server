# Migrations

One-off MongoDB data migrations, run manually.

## Naming

`NNNN_short_description.py`, numbered sequentially (`0001_...`, `0002_...`). Each script must be idempotent and never delete documents.

## Run

```bash
pip install -r scripts/requirements.txt
python3 scripts/migrations/0001_rename_extracted_expense_info.py --validate
```

Defaults: `--uri mongodb://localhost:27017`, `--db ProjectDoomsday`, `--collection files`.

## Modes (exactly one required)

- `--validate`: report old/new/both field counts; exits 1 if any doc has both (conflict).
- `--dry-run`: show count and sample `_id`s that would change; no writes.
- `--apply`: perform the change and print matched/modified counts; refuses if conflicts exist. Rerunning is a no-op.

Recommended order: `--dry-run`, `--apply`, `--validate`.
