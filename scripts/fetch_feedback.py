#!/usr/bin/env python3
"""
Fetch classification feedback from the Nimblist API and save it as JSONL
for use by retrain.py.

Usage:
    python scripts/fetch_feedback.py --api-url https://nimblist.tmnrtn.com --cookie <value>

The --cookie value is the content of the .AspNetCore.Identity.Application cookie,
obtained by logging in to the app in a browser and copying it from DevTools → Application → Cookies.

Output is newline-delimited JSON, one record per line:
    {"item_name": "...", "category": "...", "sub_category": "...", "recorded_at": "..."}
"""

import argparse
import sys

try:
    import requests
except ImportError:
    print("ERROR: 'requests' is not installed. Run: pip install requests")
    sys.exit(1)


def main():
    parser = argparse.ArgumentParser(description="Fetch Nimblist classification feedback")
    parser.add_argument('--api-url', required=True,
                        help="Base URL of the API, e.g. https://nimblist.tmnrtn.com")
    parser.add_argument('--cookie', required=True,
                        help="Value of the .AspNetCore.Identity.Application session cookie")
    parser.add_argument('--output', default='feedback.jsonl',
                        help="Output file path (default: feedback.jsonl)")
    args = parser.parse_args()

    url = args.api_url.rstrip('/') + '/api/classificationfeedback/export'
    print(f"Fetching feedback from {url} ...")

    try:
        response = requests.get(
            url,
            cookies={'.AspNetCore.Identity.Application': args.cookie},
            timeout=30,
        )
        response.raise_for_status()
    except requests.exceptions.HTTPError as e:
        print(f"ERROR: HTTP {response.status_code} — {e}")
        if response.status_code == 401:
            print("Check your --cookie value; you may need to log in again.")
        sys.exit(1)
    except requests.exceptions.ConnectionError:
        print(f"ERROR: Could not connect to {url}")
        sys.exit(1)

    lines = [l for l in response.text.strip().split('\n') if l.strip()]

    with open(args.output, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines))
        if lines:
            f.write('\n')

    print(f"Saved {len(lines)} feedback records to {args.output}")
    print(f"\nNext step — retrain the models:")
    print(f"  python scripts/retrain.py --feedback {args.output}")


if __name__ == '__main__':
    main()
