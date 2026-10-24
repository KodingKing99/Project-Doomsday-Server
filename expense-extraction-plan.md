# Expense Extraction Persistence Plan

**Status:** Approved design, not started · **Date:** 2026-09-23
**bd epic:** `ProjectDoomsdayServer-zgh` (step 0 → `.1`, 1 → `.5`, 2 → `.2`, 3+5 → `.6`, 4 → `.3`, manual script → `.4`)
**Data reference:** [docs/extracted-expense-info.md](docs/extracted-expense-info.md) · **Decision:** [ADR 0001](docs/adr/0001-extracted-expense-info-is-a-server-owned-subdocument.md) · **Glossary:** [CONTEXT.md](CONTEXT.md)

## Context

`POST /files/{id}/extractExpenseInfo` calls AWS Textract `AnalyzeExpense` and returns the result as a string — nothing is persisted, every call costs money, and with more than one `ExpenseDocument` the string is invalid JSON (objects concatenated with no separator). This plan persists the result as a typed, server-owned **Extracted Expense Info** subdocument on the **File**, with **Geometry** kept so the frontend can overlay values on the original image for user confirmation, and **RawText** kept so the data can later feed Atlas Vector Search / RAG and the CSV export.

It also catches the test suites up: **ApiTests and E2ETests currently do not compile** (`CreateFileResult` moved to `ProjectDoomsdayServer.Application.Files.Results`; tests lack the `using`), and nothing tests the extraction path.

## Where the project is now

| Area | State |
|------|-------|
| File CRUD, presigned S3 upload, per-user ownership | Done |
| Cognito JWT auth | Done, toggled by `Authentication:Enabled` |
| Textract `AnalyzeExpense` call | Working, result returned as string, not stored |
| `CsvExportService` | Stub (`NotImplementedException`) |
| ApiTests / E2ETests | **Broken build** — 12× `CS0246 CreateFileResult` |
| Extraction tests | None |

## Scope

**In:** domain model, Textract → domain mapper, persistence, cache/force behaviour, typed endpoint response, test catch-up, docs.
**Out (deferred, documented):** embeddings & vector index, auto-extract on upload, field-level `$set` updates (lost-update race), stale-cache detection, reduced list model, user corrections/confirmation workflow, CSV export.

---

## Steps

### 0. Green baseline (own commit)
- Add `using ProjectDoomsdayServer.Application.Files.Results;` to the failing ApiTests/E2ETests files (`FileMetadataTests`, `FileDeleteTests`, `FileDownloadTests`, `FileOwnershipTests`, `FilesControllerTests`, `FileCrudIntegrationTests`, `FileOwnershipE2ETests`, …).
- Fix the `CS8604` null-key warning in `CustomWebApplicationFactory.UpdateAsync` substitute.
- Replace `IFileExtractionService` with an NSubstitute in `CustomWebApplicationFactory` and a fake in `E2EWebApplicationFactory` so neither needs AWS credentials.
- `dotnet test` ApiTests + E2ETests pass. Commit: `test: fix broken test build after CreateFileResult move`.

### 1. Domain model — `ProjectDoomsdayServer.Domain`
New folder `Models/Extraction/` (POCOs, no Bson attributes):
- `ExtractedExpenseInfo { ExtractedAtUtc, Source, RawText, List<Expense> Expenses }`
- `Expense { ExpenseIndex, VendorName, VendorAddress, VendorPhone, ReceiptId, ReceiptDate, Subtotal, Tax, Total, AmountPaid, List<ExtractedField> SummaryFields, List<LineItem> LineItems }`
- `LineItem { GroupIndex, Item, Quantity, UnitPrice, Price, ProductCode, ExpenseRow, List<ExtractedField> OtherFields }`
- `ExtractedField { Type, Label?, LabelConfidence?, Text, Confidence, Geometry? }`
- `ExtractedAmountField : ExtractedField { decimal? Amount }`, `ExtractedDateField : ExtractedField { DateOnly? Date }`
- `Geometry { PageNumber, BoundingBox, List<Point> Polygon }`, `BoundingBox { Left, Top, Width, Height }`, `Point { X, Y }`

Add `public ExtractedExpenseInfo? ExtractedExpenseInfo { get; set; }` to `Domain/DB_Models/File.cs`.

### 2. Mapper — `ProjectDoomsdayServer.Infrastructure`
- `Files/TextractExpenseMapper.cs`: pure static `ExtractedExpenseInfo Map(AnalyzeExpenseResponse response, DateTimeOffset extractedAtUtc)`.
  - `RawText` = `LINE` block texts from every `ExpenseDocument.Blocks`, `"\n"`-joined.
  - Typed properties per the mapping table in the data reference; highest-confidence wins on duplicates; all summary fields kept in `SummaryFields`.
  - Flatten `LineItemGroups` → `LineItems` with `GroupIndex`.
  - Amount parsing: strip currency symbols/commas/whitespace, `decimal.TryParse` invariant → `null` on failure. Date parsing: `DateOnly.TryParseExact` over a small format list, invariant → `null` on failure.
- `FileExtractionService` returns `ExtractedExpenseInfo` via the mapper (fixes the concatenated-JSON bug). Inject `TimeProvider` for `ExtractedAtUtc`.

### 3. Ports & Application
- `IFileExtractionService.ExtractExpenseInformationAsync` → `Task<ExtractedExpenseInfo>`; update XML doc.
- `ExtractExpenseImageInfoRequest` gains `bool Force`.
- `FilesService.ExtractExpenseInformationAsync` → `Task<ExtractedExpenseInfo>`:
  1. Ownership / StorageKey checks (unchanged).
  2. If `file.ExtractedExpenseInfo is not null && !request.Force` → return it.
  3. Read stream (use `CopyToAsync` with the cancellation token), call extraction service, set `file.ExtractedExpenseInfo`, `UpdatedAtUtc`, `_repo.UpdateAsync`, return.
  4. Exceptions propagate; nothing saved.
- `IFilesService` signature + XML doc updated.
- `FilesService.UpdateAsync` is unchanged — it already copies only client-mutable fields, so `ExtractedExpenseInfo` on the loaded record survives. Add a test to lock this in.

### 4. Persistence — `MongoDbFileRepository`
- In the static class map setup, register `Decimal128` representation for `decimal`/`decimal?` (e.g. `BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128))` and the nullable equivalent) and a `DateOnly` serializer if the driver version needs one. Domain stays attribute-free.
- Register class maps for `ExtractedAmountField` / `ExtractedDateField` if polymorphic discriminators appear (they are only ever used via their concrete property types, so no discriminator is needed — verify in E2E).

### 5. WebApi — `FilesController.Extract`
- Accept `[FromQuery] bool force = false`, pass through.
- Return `ActionResult<ExtractedExpenseInfo>`; change to `[ProducesResponseType<ExtractedExpenseInfo>(StatusCodes.Status200OK)]` (keep 400/401/404).

### 6. Docs & tracking
- Keep [docs/extracted-expense-info.md](docs/extracted-expense-info.md) in sync with the final model.
- Update `doomsday_plan.md` status table + phases (done alongside this plan).
- bd epic with one issue per step.

---

## Test plan (high level)

### Fixture
The `.context/mcdonalds_receipt_output_json.json` capture is double-encoded (`McDonald\\u0027s`) and serialised from SDK types (`ConstantClass` values as `{ "Value": … }`), which won't round-trip cleanly. Re-capture a clean fixture (a small helper that dumps the `AnalyzeExpenseResponse` once against real AWS), check it into `src/ProjectDoomsdayServer.UnitTests/Fixtures/`, and write a loader that builds `AnalyzeExpenseResponse` from it. Fallback: a hand-built response covering the same cases.

### UnitTests (new project `ProjectDoomsdayServer.UnitTests`, xUnit + FluentAssertions, refs Infrastructure)
`TextractExpenseMapperTests` against the McDonald's fixture:
- Total 11.52, Subtotal 10.77, Tax 0.75, AmountPaid 11.52 (`Amount` and raw `Text`).
- `VendorName.Text` = highest-confidence `VENDOR_NAME`; both present in `SummaryFields` (22 total).
- 4 line items, correct `Item`/`Price`/`Quantity`; `UnitPrice` null; `ExpenseRow` has Geometry.
- Geometry: BoundingBox values 0–1, Polygon has 4 points, PageNumber 1; labels have no Geometry.
- `ReceiptDate.Date` null for `Jun.22'08(Sun)08:` with `Text` preserved.
- `RawText` non-empty, starts with `McDonald's Corporation`.
- Empty response → `Expenses` empty; two `ExpenseDocuments` → two Expenses.
- Amount parsing edge cases (`"$1,234.50"`, `"abc"`, `""`).

### ApiTests (mocked `IFileExtractionService`, no Docker)
- Extract returns 200 with typed body and stores it on the record.
- Second call returns cached value; service received exactly one call.
- `?force=true` calls service again and overwrites.
- Other user's file → 404; no StorageKey → 404.
- Zero expenses stored as `Expenses: []` and cached.
- Service throws → record unchanged.
- `PUT /files/{id}` after extraction keeps `ExtractedExpenseInfo`.
- `GET /files/{id}` and `GET /files` include `ExtractedExpenseInfo`.

### E2ETests (real Mongo + MinIO, faked extraction service)
- Extract → re-GET returns identical `ExtractedExpenseInfo` (full round-trip incl. Geometry).
- Amounts stored as `Decimal128` in Mongo (query raw BSON).
- Cache hit after app restart / new factory (persisted, not in-memory).
- PUT after extract preserves the subdocument in Mongo.

### Not automated
Live Textract. Add a manual script alongside `scripts/uploadToS3.py` that uploads a receipt and calls extract.

---

## Verification
1. `dotnet csharpier format .` && `dotnet build`
2. `dotnet test --filter "FullyQualifiedName~UnitTests" --verbosity minimal 2>&1 | head -n 150`
3. `dotnet test --filter "FullyQualifiedName~ApiTests" --verbosity minimal 2>&1 | head -n 150`
4. `dotnet test --filter "FullyQualifiedName~E2ETests" --verbosity minimal 2>&1 | head -n 150` (Docker)
5. Manual: run the API against dev AWS, upload the McDonald's image, `POST /files/{id}/extractExpenseInfo` twice (second returns instantly, no Textract call in CloudWatch), inspect the document in Mongo (`ExtractedExpenseInfo.Expenses[0].Total.Amount` is `NumberDecimal("11.52")`).

## Known limitations carried forward
See [docs/extracted-expense-info.md § Known limitations](docs/extracted-expense-info.md#known-limitations-deferred): lost-update race on full-document replace, cache assumes immutable bytes, list payload includes Geometry, no embeddings yet.
