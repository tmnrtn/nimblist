#!/usr/bin/env python3
"""
Retrain the Nimblist classification models, optionally merging user feedback.

Usage:
    python scripts/retrain.py
    python scripts/retrain.py --feedback feedback.jsonl
    python scripts/retrain.py --feedback feedback.jsonl --feedback-repeat 10
    python scripts/retrain.py --training-data path/to/combined_cleaned.csv --output-dir path/to/classification/

The script retrains the primary category model and all per-category sub-models using
the same Logistic Regression + TF-IDF architecture as the original training notebooks.

Improvements over the original notebook pipeline:
- max_features increased from 5,000 to 15,000 for better vocabulary coverage
- Feedback rows are oversampled (default 5x) since they are verified ground truth
- A classification report is printed so you can compare accuracy before/after
"""

import argparse
import json
import os
import re
import sys
import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import classification_report, accuracy_score
import joblib

# ---------------------------------------------------------------------------
# Paths — script lives in scripts/, models live in src/nimblist/Nimblist.classification/
# ---------------------------------------------------------------------------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPT_DIR)
DEFAULT_TRAINING_CSV = os.path.join(SCRIPT_DIR, "ClassificationModel", "combined_cleaned.csv")
DEFAULT_OUTPUT_DIR = os.path.join(REPO_ROOT, "src", "nimblist", "Nimblist.classification")
SUB_MODELS_SUBDIR = "sub_category_models"


# ---------------------------------------------------------------------------
# Text cleaning — MUST stay identical to app.py's clean_text()
# ---------------------------------------------------------------------------
def clean_text(text: str) -> str:
    if not isinstance(text, str):
        text = str(text)
    text = text.lower()
    text = re.sub(r'[^\w\s]', '', text)
    text = re.sub(r'\s+', ' ', text).strip()
    return text


def sanitize_filename(name: str) -> str:
    name = re.sub(r'[^\w\-]+', '_', name)
    return name.strip('_')


# ---------------------------------------------------------------------------
# Data loading
# ---------------------------------------------------------------------------
def load_base_training_data(csv_path: str) -> pd.DataFrame:
    print(f"Loading base training data from: {csv_path}")
    df = pd.read_csv(csv_path)
    df.dropna(subset=['generic_product_name', 'newCat', 'newSubCat'], inplace=True)
    print(f"  {len(df):,} rows after dropping nulls")
    return df[['generic_product_name', 'newCat', 'newSubCat']].copy()


def load_feedback(jsonl_path: str) -> pd.DataFrame:
    """
    Load feedback exported from GET /api/classificationfeedback/export.
    Each line is JSON: {item_name, category, sub_category, recorded_at}
    Rows with null category are skipped (user removed the classification).
    """
    records = []
    with open(jsonl_path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
                if obj.get('category'):
                    records.append({
                        'generic_product_name': obj['item_name'],
                        'newCat': obj['category'],
                        'newSubCat': obj.get('sub_category') or '',
                    })
            except json.JSONDecodeError as e:
                print(f"  Warning: skipping malformed line: {e}")
    print(f"  {len(records):,} usable feedback records (category != null)")
    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Training helpers
# ---------------------------------------------------------------------------
def train_primary_model(df: pd.DataFrame):
    X_cleaned = df['generic_product_name'].apply(clean_text)
    y = df['newCat']

    X_train, X_test, y_train, y_test = train_test_split(
        X_cleaned, y, test_size=0.20, random_state=42, stratify=y
    )

    vectorizer = TfidfVectorizer(
        stop_words='english',
        max_features=15000,   # up from 5,000 in the original notebooks
        ngram_range=(1, 2),
    )
    print("  Fitting primary TF-IDF vectorizer...")
    vectorizer.fit(X_train)
    X_train_tfidf = vectorizer.transform(X_train)
    X_test_tfidf = vectorizer.transform(X_test)

    print("  Training primary LogisticRegression...")
    model = LogisticRegression(class_weight='balanced', max_iter=1000, random_state=42)
    model.fit(X_train_tfidf, y_train)

    y_pred = model.predict(X_test_tfidf)
    acc = accuracy_score(y_test, y_pred)
    print(f"\n  Primary model test accuracy: {acc:.3f}")
    print(classification_report(y_test, y_pred, zero_division=0))

    return model, vectorizer


def train_sub_models(df: pd.DataFrame):
    sub_models = {}
    sub_vectorizers = {}

    for primary_cat in df['newCat'].unique():
        df_sub = df[df['newCat'] == primary_cat].copy()
        df_sub = df_sub[df_sub['newSubCat'].str.strip() != '']

        if len(df_sub) < 10:
            print(f"  Skipping '{primary_cat}': only {len(df_sub)} samples")
            continue
        if df_sub['newSubCat'].nunique() < 2:
            print(f"  Skipping '{primary_cat}': only one sub-category")
            continue

        X = df_sub['generic_product_name'].apply(clean_text)
        y = df_sub['newSubCat']

        try:
            X_train, X_test, y_train, y_test = train_test_split(
                X, y, test_size=0.20, random_state=42, stratify=y
            )
        except ValueError:
            X_train, X_test, y_train, y_test = train_test_split(
                X, y, test_size=0.20, random_state=42
            )

        vec = TfidfVectorizer(stop_words='english', max_features=15000, ngram_range=(1, 2))
        vec.fit(X_train)
        X_train_tfidf = vec.transform(X_train)
        X_test_tfidf = vec.transform(X_test)

        mdl = LogisticRegression(class_weight='balanced', max_iter=1000, random_state=42)
        mdl.fit(X_train_tfidf, y_train)

        acc = accuracy_score(y_test, mdl.predict(X_test_tfidf))
        print(f"  '{primary_cat}' sub-model — {len(df_sub):,} samples, {df_sub['newSubCat'].nunique()} sub-cats, acc={acc:.3f}")

        key = sanitize_filename(primary_cat)
        sub_models[key] = mdl
        sub_vectorizers[key] = vec

    return sub_models, sub_vectorizers


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(description="Retrain Nimblist classification models")
    parser.add_argument('--training-data', default=DEFAULT_TRAINING_CSV,
                        help=f"Path to combined_cleaned.csv (default: {DEFAULT_TRAINING_CSV})")
    parser.add_argument('--feedback', default=None,
                        help="Path to JSONL feedback file from GET /api/classificationfeedback/export")
    parser.add_argument('--feedback-repeat', type=int, default=5,
                        help="How many times to repeat each feedback row (default: 5)")
    parser.add_argument('--output-dir', default=DEFAULT_OUTPUT_DIR,
                        help=f"Where to save model files (default: {DEFAULT_OUTPUT_DIR})")
    args = parser.parse_args()

    # ------------------------------------------------------------------
    # Load and merge data
    # ------------------------------------------------------------------
    if not os.path.exists(args.training_data):
        print(f"ERROR: Training data not found at {args.training_data}")
        print("Ensure combined_cleaned.csv is in scripts/ClassificationModel/")
        sys.exit(1)

    df = load_base_training_data(args.training_data)

    if args.feedback:
        if not os.path.exists(args.feedback):
            print(f"ERROR: Feedback file not found at {args.feedback}")
            sys.exit(1)
        print(f"Loading feedback from: {args.feedback}")
        fb = load_feedback(args.feedback)
        if len(fb) > 0:
            repeated = pd.concat([fb] * args.feedback_repeat, ignore_index=True)
            df = pd.concat([df, repeated], ignore_index=True)
            print(f"  Dataset after merging feedback ({args.feedback_repeat}x repeat): {len(df):,} rows")
        else:
            print("  No usable feedback found; training on base data only.")
    else:
        print("No feedback file provided; training on base data only.")

    print(f"\nTotal training rows: {len(df):,}")
    print(f"Primary categories: {sorted(df['newCat'].unique())}")

    # ------------------------------------------------------------------
    # Train models
    # ------------------------------------------------------------------
    print("\n=== Training primary model ===")
    primary_model, primary_vectorizer = train_primary_model(df)

    print("\n=== Training sub-category models ===")
    sub_models, sub_vectorizers = train_sub_models(df)

    # ------------------------------------------------------------------
    # Save
    # ------------------------------------------------------------------
    sub_dir = os.path.join(args.output_dir, SUB_MODELS_SUBDIR)
    os.makedirs(sub_dir, exist_ok=True)

    primary_model_path = os.path.join(args.output_dir, 'supermarket_classifier_logreg.joblib')
    primary_vec_path = os.path.join(args.output_dir, 'tfidf_vectorizer_logreg.joblib')
    print(f"\nSaving primary model → {primary_model_path}")
    joblib.dump(primary_model, primary_model_path)
    print(f"Saving primary vectorizer → {primary_vec_path}")
    joblib.dump(primary_vectorizer, primary_vec_path)

    for key, mdl in sub_models.items():
        mdl_path = os.path.join(sub_dir, f'model_sub_{key}.joblib')
        vec_path = os.path.join(sub_dir, f'vectorizer_sub_{key}.joblib')
        joblib.dump(mdl, mdl_path)
        joblib.dump(sub_vectorizers[key], vec_path)
    print(f"Saved {len(sub_models)} sub-models to {sub_dir}")

    print("\nDone. Restart the classification service container to pick up new models:")
    print("  docker compose restart Nimblist.classification")


if __name__ == '__main__':
    main()
