#!/usr/bin/env python3
"""Compare stored Remito rates with a second-source CSV export."""

import argparse
import csv
import sys
from decimal import Decimal, InvalidOperation


def read_rates(path: str) -> dict[str, Decimal]:
    with open(path, newline="", encoding="utf-8-sig") as source:
        reader = csv.DictReader(source)
        if reader.fieldnames is None or not {"pair", "mid"}.issubset(
            {name.lower() for name in reader.fieldnames}
        ):
            raise ValueError(f"{path}: CSV must contain pair and mid columns")

        columns = {name.lower(): name for name in reader.fieldnames}
        rates: dict[str, Decimal] = {}
        for row in reader:
            pair = row[columns["pair"]].strip().upper().replace("-", "/")
            if "/" not in pair and len(pair) == 6:
                pair = f"{pair[:3]}/{pair[3:]}"
            try:
                mid = Decimal(row[columns["mid"]])
            except (InvalidOperation, TypeError) as error:
                raise ValueError(f"{path}: invalid mid for {pair}") from error
            if not mid.is_finite() or mid <= 0:
                raise ValueError(f"{path}: mid for {pair} must be finite and positive")
            rates[pair] = mid
        return rates


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stored", required=True, help="SQL-exported rates CSV with pair,mid columns")
    parser.add_argument("--comparison", required=True, help="Second-source rates CSV with pair,mid columns")
    parser.add_argument("--threshold-percent", type=Decimal, default=Decimal("1"))
    args = parser.parse_args()
    if args.threshold_percent < 0:
        parser.error("--threshold-percent must be non-negative")

    stored = read_rates(args.stored)
    comparison = read_rates(args.comparison)
    if not stored or not comparison:
        parser.error("both CSV files must contain at least one rate")

    variances: list[tuple[str, Decimal, Decimal, Decimal]] = []
    missing_count = 0
    for pair, stored_mid in sorted(stored.items()):
        comparison_mid = comparison.get(pair)
        if comparison_mid is None:
            missing_count += 1
            print(f"MISSING,{pair},stored_mid={stored_mid}")
            continue
        variance = abs(stored_mid - comparison_mid) / comparison_mid * Decimal(100)
        if variance > args.threshold_percent:
            variances.append((pair, stored_mid, comparison_mid, variance))
            print(
                f"VARIANCE,{pair},stored_mid={stored_mid},"
                f"comparison_mid={comparison_mid},variance_percent={variance:.4f}"
            )

    if not variances and missing_count == 0:
        print(f"PASS,no variance exceeded {args.threshold_percent}%")
        return 0
    print(
        f"FAIL,{len(variances)} pair(s) exceeded {args.threshold_percent}%,"
        f"{missing_count} stored pair(s) had no comparison value"
    )
    return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError) as error:
        print(f"ERROR,{error}", file=sys.stderr)
        sys.exit(2)
