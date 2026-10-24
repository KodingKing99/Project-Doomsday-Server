# Extracted Expense Info is a server-owned subdocument on File, not Metadata

The original intent was to dump extraction output into `File.Metadata`. We instead store it as a typed `ExtractedExpenseInfo` subdocument on the File record, because `Metadata` is a client-owned `Dictionary<string, string>` that `PUT /files/{id}` clears and replaces wholesale — anything the server put there would be silently erased on the next client update, would be editable by the client, and would be an opaque string Mongo can't query or aggregate inside.

## Considered Options

- **JSON string under a reserved `Metadata` key** — no schema change, but clobbered by PUT, client-editable, and not queryable.
- **Flattened `Metadata` keys** (`expense.total`, …) — lossy (line items and Geometry don't fit), everything becomes a string.
- **Separate `expenseExtractions` collection keyed by fileId** — only worth it if the payload grows too large for the File document; not the case at ~10–30 KB per receipt.

## Consequences

- The raw text for future RAG / Atlas Vector Search lives in `ExtractedExpenseInfo.RawText`, not in Metadata, for the same reasons.
- `GET /files` returns the subdocument (including Geometry) until a reduced list model is introduced.
- Moving this later means a data migration across every extracted File.
