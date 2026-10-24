# Project Doomsday

A personal finance document store. Users upload receipts and other financial documents, the server extracts the expense data from them, and that data is later exported (CSV) and made queryable (vector search / RAG).

## Language

### Files

**File**:
An uploaded document owned by one user, together with everything known about it.
_Avoid_: Document, upload, record

**Metadata**:
Free-form key/value pairs supplied and owned by the client; the client may replace them at any time.
_Avoid_: Using "metadata" for anything the server derives from the file's contents

### Expense extraction

**Extracted Expense Info**:
Everything the server derived from a File's contents by running expense extraction on it. Server-owned; the client cannot edit it.s
_Avoid_: Metadata, extraction result, analysis

**Expense**:
One receipt or invoice found within a File. A File usually holds one Expense but may hold several (e.g. two receipts scanned onto one page).
_Avoid_: Receipt (as the model name), transaction, purchase

**Summary Field**:
A single labelled value read from an Expense as a whole, such as its vendor, date, tax or total.
_Avoid_: Header field, property

**Line Item**:
One purchased row on an Expense: the item, its quantity, its unit price, and its price.
_Avoid_: Row, entry, product

**Price**:
The total amount for a Line Item (quantity × unit price), not the price of a single unit.
_Avoid_: Amount, cost

**Unit Price**:
The price of a single unit of a Line Item. There is no unit of measure (lb, ea) in the model.
_Avoid_: Unit (on its own)

**Geometry**:
Where on the page a piece of extracted text was found, kept so the source can be highlighted on the original image for the user to confirm.
_Avoid_: Coordinates, position, location
