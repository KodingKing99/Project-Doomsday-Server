#!/usr/bin/env python3
"""Rename File field ExtractedExpenseInfo to ExtractionInfo in place."""
import argparse
import sys

from pymongo import MongoClient

OLD_FIELD = "ExtractedExpenseInfo"
NEW_FIELD = "ExtractionInfo"
SAMPLE_SIZE = 10

ONLY_OLD_FILTER = {OLD_FIELD: {"$exists": True}, NEW_FIELD: {"$exists": False}}
BOTH_FILTER = {OLD_FIELD: {"$exists": True}, NEW_FIELD: {"$exists": True}}


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--uri", default="mongodb://localhost:27017")
    parser.add_argument("--db", default="ProjectDoomsday")
    parser.add_argument("--collection", default="files")
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--validate", action="store_true", help="report counts; exit 1 on conflicts")
    mode.add_argument("--dry-run", action="store_true", help="show what would change; no writes")
    mode.add_argument("--apply", action="store_true", help="perform the rename")
    return parser.parse_args()


def count_states(collection):
    return {
        "old": collection.count_documents({OLD_FIELD: {"$exists": True}}),
        "new": collection.count_documents({NEW_FIELD: {"$exists": True}}),
        "both": collection.count_documents(BOTH_FILTER),
    }


def print_counts(counts):
    print(f"Docs with {OLD_FIELD} (old): {counts['old']}")
    print(f"Docs with {NEW_FIELD} (new): {counts['new']}")
    print(f"Docs with BOTH (conflict):  {counts['both']}")


def validate(collection):
    counts = count_states(collection)
    print_counts(counts)
    if counts["both"] > 0:
        print("CONFLICT: resolve docs with both fields manually.")
        return 1
    return 0


def dry_run(collection):
    affected = collection.count_documents(ONLY_OLD_FILTER)
    conflicts = collection.count_documents(BOTH_FILTER)
    print(f"Would rename {OLD_FIELD} -> {NEW_FIELD} on {affected} doc(s).")
    sample_ids = [doc["_id"] for doc in collection.find(ONLY_OLD_FILTER, {"_id": 1}).limit(SAMPLE_SIZE)]
    print(f"Sample _ids (up to {SAMPLE_SIZE}): {sample_ids}")
    if conflicts > 0:
        print(f"WARNING: {conflicts} conflict doc(s); --apply would refuse to run.")
        return 1
    return 0


def apply(collection):
    conflicts = collection.count_documents(BOTH_FILTER)
    if conflicts > 0:
        print(f"Refusing to apply: {conflicts} doc(s) have both fields.")
        return 1
    result = collection.update_many(ONLY_OLD_FILTER, {"$rename": {OLD_FIELD: NEW_FIELD}})
    print(f"Matched: {result.matched_count}  Modified: {result.modified_count}")
    return 0


def main():
    args = parse_args()
    collection = MongoClient(args.uri, serverSelectionTimeoutMS=5000)[args.db][args.collection]
    print(f"Target: {args.uri} / {args.db}.{args.collection}")
    if args.validate:
        return validate(collection)
    if args.dry_run:
        return dry_run(collection)
    return apply(collection)


if __name__ == "__main__":
    sys.exit(main())
