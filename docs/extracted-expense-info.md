# Extracted Expense Info — Data Reference

What the server stores on a **File** after running expense extraction (AWS Textract `AnalyzeExpense`). Vocabulary follows [CONTEXT.md](../CONTEXT.md); the storage decision is recorded in [ADR 0001](adr/0001-extracted-expense-info-is-a-server-owned-subdocument.md).

- Lives at `File.ExtractedExpenseInfo` in the `files` Mongo collection (`null` until extraction has run).
- **Server-owned.** Written only by `POST /files/{id}/extractExpenseInfo`; `PUT /files/{id}` never reads it from the request body.
- **Not** stored in `File.Metadata` — that dictionary belongs to the client and is replaced on every PUT.

---

## Shape

```
File
└── ExtractedExpenseInfo?
    ├── ExtractedAtUtc      DateTimeOffset
    ├── Source              string            "AwsTextract.AnalyzeExpense"
    ├── RawText             string            all LINE text, reading order, "\n"-joined (for RAG later)
    └── Expenses[]          Expense           usually 1; [] when nothing was found
        ├── ExpenseIndex    int               Textract's 1-based index
        ├── VendorName      ExtractedField?
        ├── VendorAddress   ExtractedField?
        ├── VendorPhone     ExtractedField?
        ├── ReceiptId       ExtractedField?
        ├── ReceiptDate     ExtractedDateField?
        ├── Subtotal        ExtractedAmountField?
        ├── Tax             ExtractedAmountField?
        ├── Total           ExtractedAmountField?
        ├── AmountPaid      ExtractedAmountField?
        ├── SummaryFields[] ExtractedField    lossless: every summary field Textract returned
        └── LineItems[]     LineItem
            ├── GroupIndex  int               Textract LineItemGroupIndex (groups are flattened)
            ├── Item        ExtractedField?
            ├── Quantity    ExtractedAmountField?
            ├── UnitPrice   ExtractedAmountField?
            ├── Price       ExtractedAmountField?   line total, NOT unit price
            ├── ProductCode ExtractedField?
            ├── ExpenseRow  ExtractedField?   the whole row's text + Geometry
            └── OtherFields[] ExtractedField  any line-item field not mapped above
```

### Building blocks

| Type | Fields | Notes |
|------|--------|-------|
| `ExtractedField` | `Type`, `Label?`, `LabelConfidence?`, `Text`, `Confidence`, `Geometry?` | `Type` is Textract's field type (`TOTAL`, `VENDOR_NAME`, `OTHER`…). `Label` is the printed label if any (`"SUB TOTAL"`). `Text` is exactly what was read — never altered. Confidence is 0–100. |
| `ExtractedAmountField` | `ExtractedField` + `Amount: decimal?` | `Amount` parsed from `Text` (invariant culture, currency symbols/commas stripped). `null` when unparseable. Stored as Mongo `Decimal128`. |
| `ExtractedDateField` | `ExtractedField` + `Date: DateOnly?` | Parsed with a small set of common receipt formats. `null` when unparseable — the raw `Text` is always kept. |
| `Geometry` | `PageNumber`, `BoundingBox { Left, Top, Width, Height }`, `Polygon[] { X, Y }` | Normalized 0–1 as a fraction of page width/height. Multiply by the rendered image size to draw an overlay. Polygon follows skewed/rotated text; BoundingBox is the axis-aligned rectangle. Geometry is stored for the **value** only, not the label. |

### Mapping rules

| Typed property | Textract summary field type |
|----------------|-----------------------------|
| `VendorName` | `VENDOR_NAME` |
| `VendorAddress` | `VENDOR_ADDRESS` |
| `VendorPhone` | `VENDOR_PHONE` |
| `ReceiptId` | `INVOICE_RECEIPT_ID` |
| `ReceiptDate` | `INVOICE_RECEIPT_DATE` |
| `Subtotal` | `SUBTOTAL` |
| `Tax` | `TAX` |
| `Total` | `TOTAL` |
| `AmountPaid` | `AMOUNT_PAID` |

| Line item property | Textract line item field type |
|--------------------|-------------------------------|
| `Item` | `ITEM` |
| `Quantity` | `QUANTITY` |
| `UnitPrice` | `UNIT_PRICE` |
| `Price` | `PRICE` |
| `ProductCode` | `PRODUCT_CODE` |
| `ExpenseRow` | `EXPENSE_ROW` |

- When a type appears more than once (the sample has two `VENDOR_NAME`s), the typed property takes the **highest-confidence** one. All occurrences remain in `SummaryFields[]`.
- Every summary field — mapped or not (`OTHER`, `STREET`, `CITY`, card details…) — is kept in `SummaryFields[]`.
- Textract `Blocks` are not stored, except their `LINE` text, which becomes `RawText`.
- Masked card numbers and auth codes printed on the receipt are stored as-is in `SummaryFields[]` (accepted).

---

## Example — McDonald's receipt

Source: `.context/mcdonalds_receipt_output_json.json` (raw Textract output, ~227 KB). Stored subdocument, abridged (`…` marks omitted repetition; polygons trimmed to one entry):

```json
{
  "ExtractedExpenseInfo": {
    "ExtractedAtUtc": "2026-09-23T17:04:11Z",
    "Source": "AwsTextract.AnalyzeExpense",
    "RawText": "McDonald's Corporation\nThank you for eating at McDonald's\n1027 NORTH HIGH STREET\nMILLVILLE, NJ 08332-2526\n…\nTEL# (856)327-0606\n…\nJun.22'08(Sun)08:56\nSTORE# 6038\nMER# KB34463365001\n…",
    "Expenses": [
      {
        "ExpenseIndex": 1,
        "VendorName": {
          "Type": "VENDOR_NAME",
          "Label": null,
          "LabelConfidence": null,
          "Text": "McDonald's",
          "Confidence": 99.99,
          "Geometry": {
            "PageNumber": 1,
            "BoundingBox": { "Left": 0.5951, "Top": 0.2848, "Width": 0.1271, "Height": 0.0214 },
            "Polygon": [ { "X": 0.5951, "Y": 0.2848 }, "…" ]
          }
        },
        "ReceiptId": { "Type": "INVOICE_RECEIPT_ID", "Label": "MER#", "Text": "KB34463365001", "…": "…" },
        "ReceiptDate": {
          "Type": "INVOICE_RECEIPT_DATE",
          "Text": "Jun.22'08(Sun)08:",
          "Confidence": 79.14,
          "Date": null,
          "Geometry": { "…": "…" }
        },
        "Subtotal": { "Type": "SUBTOTAL", "Label": "SUB TOTAL", "Text": "10.77", "Amount": 10.77, "Confidence": 99.99, "Geometry": { "…": "…" } },
        "Tax":      { "Type": "TAX", "Label": "TAKE OUT TAX", "Text": "0.75", "Amount": 0.75, "…": "…" },
        "Total": {
          "Type": "TOTAL",
          "Text": "11.52",
          "Amount": 11.52,
          "Confidence": 99.99,
          "Geometry": {
            "PageNumber": 1,
            "BoundingBox": { "Left": 0.6766, "Top": 0.7576, "Width": 0.0610, "Height": 0.0194 },
            "Polygon": [ { "X": 0.6774, "Y": 0.7576 }, "…" ]
          }
        },
        "AmountPaid": { "Type": "AMOUNT_PAID", "Label": "TRANSACTION AMOUNT", "Text": "11.52", "Amount": 11.52, "…": "…" },
        "VendorPhone": { "Type": "VENDOR_PHONE", "Label": "TEL#", "Text": "(856)327-0606", "…": "…" },
        "SummaryFields": [
          { "Type": "ADDRESS", "Text": "1027 NORTH HIGH STREET\nMILLVILLE, NJ 08332-2526", "…": "…" },
          { "Type": "VENDOR_NAME", "Text": "McDonald's Corporation", "Confidence": 99.97, "…": "…" },
          { "Type": "OTHER", "Label": "STORE#", "Text": "6038", "…": "…" },
          "… 22 entries total"
        ],
        "LineItems": [
          {
            "GroupIndex": 1,
            "Item":       { "Type": "ITEM", "Text": "BIG BREAKFAST", "Confidence": 99.99, "Geometry": { "…": "…" } },
            "Quantity":   { "Type": "QUANTITY", "Text": "1", "Amount": 1, "…": "…" },
            "UnitPrice":  null,
            "Price":      { "Type": "PRICE", "Text": "2.99", "Amount": 2.99, "…": "…" },
            "ProductCode": null,
            "ExpenseRow": { "Type": "EXPENSE_ROW", "Text": "1 BIG BREAKFAST 2.99", "Geometry": { "…": "…" } },
            "OtherFields": []
          },
          { "GroupIndex": 1, "Item": { "Text": "HOTCAKES", "…": "…" }, "Price": { "Amount": 2.29, "…": "…" }, "…": "…" },
          { "GroupIndex": 1, "Item": { "Text": "2 EGG MCMUFFIN", "…": "…" }, "Price": { "Amount": 3.00, "…": "…" }, "…": "…" },
          { "GroupIndex": 1, "Item": { "Text": "SAU EGG CH MCGRIDDLE", "…": "…" }, "Price": { "Amount": 2.49, "…": "…" }, "…": "…" }
        ]
      }
    ]
  }
}
```

---

## Behaviour

| Situation | Result |
|-----------|--------|
| First `POST /files/{id}/extractExpenseInfo` | Calls Textract, stores `ExtractedExpenseInfo`, returns it (200). |
| Called again | Returns the stored value without calling Textract. |
| Called with `?force=true` | Calls Textract again and overwrites the stored value. |
| Textract finds nothing (e.g. not a receipt) | Stored with `Expenses: []`; cached like any other result. |
| Textract throws | Nothing is stored; error propagates to the client. |
| File not found / not owned / no StorageKey | 404. |
| `PUT /files/{id}` | Does not touch `ExtractedExpenseInfo` (see known limitations). |
| `DELETE /files/{id}` | Removed with the File. |

## Known limitations (deferred)

- **Lost-update race.** `IFileRepository.UpdateAsync` replaces the whole document. A PUT that loaded the File before an extraction finished will overwrite (erase) the new `ExtractedExpenseInfo`, and vice versa. Fix later with field-level `$set` updates.
- **Cache assumes immutable file bytes.** The presigned upload URL can be reused until it expires; re-uploading different bytes to the same StorageKey leaves a stale extraction. Use `?force=true`.
- **List payload size.** `GET /files` returns full `ExtractedExpenseInfo` including Geometry (~10–30 KB per receipt). A reduced list model (vendor, total, date) is planned.
- **No embeddings yet.** `RawText` is stored ready for Atlas Vector Search / RAG; generating embeddings and the vector index are a later phase.
