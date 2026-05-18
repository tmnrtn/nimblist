#!/usr/bin/env python3
"""
Retrain the Nimblist classification models, optionally merging user feedback.

Usage:
    python scripts/retrain.py
    python scripts/retrain.py --feedback feedback.jsonl
    python scripts/retrain.py --feedback feedback.jsonl --feedback-repeat 10
    python scripts/retrain.py --no-augmentation
    python scripts/retrain.py --training-data path/to/combined_cleaned.csv --output-dir path/to/classification/

Pipeline improvements over the original training notebooks:
  - Quantity/size tokens (500g, 2L, 6 pack, x4) stripped from product names
  - Rule-based lemmatization (eggs->egg, tomatoes->tomato) — no external dependencies
  - Data augmentation: shorter left-truncated versions of each name are added as
    training examples, so "organic whole milk" also trains on "whole milk" and "milk"
  - TF-IDF max_features increased from 5,000 to 15,000; sublinear_tf=True
  - Final deployment models are trained on ALL data; a separate evaluation pass
    on a held-out split gives honest accuracy metrics
  - Feedback rows are oversampled (default 5x) as verified ground truth

clean_text() and _lemmatize_word() below MUST stay identical to the copies in
src/nimblist/Nimblist.classification/app.py — they define the shared preprocessing
contract between training and inference.
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
# Paths
# ---------------------------------------------------------------------------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPT_DIR)
DEFAULT_TRAINING_CSV = os.path.join(SCRIPT_DIR, 'ClassificationModel', 'combined_cleaned.csv')
DEFAULT_OUTPUT_DIR = os.path.join(REPO_ROOT, 'src', 'nimblist', 'Nimblist.classification')
SUB_MODELS_SUBDIR = 'sub_category_models'

# ---------------------------------------------------------------------------
# Text preprocessing — MUST stay identical to app.py's versions
# ---------------------------------------------------------------------------

def _lemmatize_word(word: str) -> str:
    """Rule-based lemmatization for common grocery plural forms. No external dependencies."""
    if len(word) <= 2:
        return word
    # ies -> y: berries->berry, pastries->pastry, strawberries->strawberry
    if len(word) > 4 and word.endswith('ies'):
        return word[:-3] + 'y'
    # ves -> f: loaves->loaf, halves->half
    if len(word) > 4 and word.endswith('ves'):
        return word[:-3] + 'f'
    # oes -> o: tomatoes->tomato, potatoes->potato, mangoes->mango
    if len(word) > 5 and word.endswith('oes'):
        return word[:-2]
    # Standard plural s: eggs->egg, biscuits->biscuit, carrots->carrot
    # Skip: ss endings (grass), us endings (asparagus), is endings (basis)
    if (word.endswith('s')
            and not word.endswith('ss')
            and not word.endswith('us')
            and not word.endswith('is')
            and len(word) > 3):
        return word[:-1]
    return word


def clean_text(text: str) -> str:
    """
    Normalise a product name or user-typed item for classification.
    Applied identically at training time (here) and inference time (app.py).
    """
    text = str(text).lower()
    # Strip size/quantity patterns before removing punctuation so word
    # boundaries still work against digit+unit tokens.
    text = re.sub(r'\b\d+(\.\d+)?\s*(g|kg|ml|l|lb|oz|cl|mg|litre|litres|liter|liters|pint|pints)\b',
                  ' ', text, flags=re.IGNORECASE)
    text = re.sub(r'\b\d+\s*x\s*\d+(\.\d+)?\s*(g|kg|ml|l|lb|oz|cl|mg)?\b', ' ', text)  # 4 x 500g
    text = re.sub(r'\bx\d+\b', ' ', text)                           # x6
    text = re.sub(r'\b\d+\s*pack\b', ' ', text, flags=re.IGNORECASE)          # 6 pack
    text = re.sub(r'\bpack\s+of\s+\d+\b', ' ', text, flags=re.IGNORECASE)     # pack of 12
    text = re.sub(r'[^\w\s]', '', text)            # remove punctuation
    text = re.sub(r'\s+', ' ', text).strip()       # normalise whitespace
    text = ' '.join(_lemmatize_word(w) for w in text.split())
    return text

# ---------------------------------------------------------------------------
# Data loading
# ---------------------------------------------------------------------------

def load_base_training_data(csv_path: str) -> pd.DataFrame:
    print(f'Loading base training data from: {csv_path}')
    df = pd.read_csv(csv_path)
    before = len(df)
    df.dropna(subset=['generic_product_name', 'newCat', 'newSubCat'], inplace=True)
    print(f'  {len(df):,} rows ({before - len(df):,} dropped for nulls)')
    return df[['generic_product_name', 'newCat', 'newSubCat']].copy()


def load_feedback(jsonl_path: str) -> pd.DataFrame:
    """
    Load feedback from GET /api/classificationfeedback/export.
    Each line: {item_name, category, sub_category, recorded_at}
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
                print(f'  Warning: skipping malformed line: {e}')
    print(f'  {len(records):,} usable feedback records (category != null)')
    return pd.DataFrame(records)

# ---------------------------------------------------------------------------
# Data augmentation
# ---------------------------------------------------------------------------

def augment_training_data(df: pd.DataFrame, max_drop: int = 2) -> pd.DataFrame:
    """
    Generate shorter training examples by dropping words from the left of each
    cleaned product name. Left-most words in product descriptions tend to be
    qualifiers ('organic', 'free range') while the category-bearing noun sits
    toward the right — so left-truncation produces examples that look like
    what users actually type ('whole milk', 'milk' from 'organic whole milk').

    max_drop=2 means each name produces at most 2 shorter variants.
    """
    rows = []
    for _, row in df.iterrows():
        words = row['generic_product_name'].split()
        for drop in range(1, min(max_drop + 1, len(words))):
            shorter = ' '.join(words[drop:]).strip()
            if shorter:
                rows.append({
                    'generic_product_name': shorter,
                    'newCat': row['newCat'],
                    'newSubCat': row['newSubCat'],
                })
    return pd.DataFrame(rows)

# ---------------------------------------------------------------------------
# Vectorizer / model helpers
# ---------------------------------------------------------------------------

def _make_vectorizer() -> TfidfVectorizer:
    return TfidfVectorizer(
        stop_words='english',
        max_features=15_000,
        ngram_range=(1, 2),
        sublinear_tf=True,
    )


def _make_model() -> LogisticRegression:
    return LogisticRegression(class_weight='balanced', max_iter=1000, random_state=42)

# ---------------------------------------------------------------------------
# Training
# ---------------------------------------------------------------------------

def train_primary_model(X: pd.Series, y: pd.Series):
    """
    Two-pass training:
      Pass 1 — evaluation: vectorizer fit on train split only (honest accuracy metrics).
      Pass 2 — deployment: vectorizer fit on ALL data (maximum vocabulary coverage).
    Returns the deployment (all-data) model and vectorizer.
    """
    # Pass 1: evaluation
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.20, random_state=42, stratify=y
    )
    vec_eval = _make_vectorizer()
    vec_eval.fit(X_train)
    mdl_eval = _make_model()
    mdl_eval.fit(vec_eval.transform(X_train), y_train)
    y_pred = mdl_eval.predict(vec_eval.transform(X_test))
    acc = accuracy_score(y_test, y_pred)
    print(f'\n  Evaluation accuracy (held-out 20%): {acc:.3f}')
    print(classification_report(y_test, y_pred, zero_division=0))

    # Pass 2: deployment — fit vectorizer on all data
    print('  Training deployment model on full dataset...')
    vec_final = _make_vectorizer()
    vec_final.fit(X)
    mdl_final = _make_model()
    mdl_final.fit(vec_final.transform(X), y)

    return mdl_final, vec_final


def train_sub_models(df: pd.DataFrame):
    """
    Train one LogReg sub-classifier per primary category.
    Uses the same two-pass approach: evaluation on a held-out split,
    then deployment model fit on all data for that category.
    """
    sub_models = {}
    sub_vectorizers = {}

    for primary_cat in sorted(df['newCat'].unique()):
        df_sub = df[df['newCat'] == primary_cat].copy()
        df_sub = df_sub[df_sub['newSubCat'].str.strip() != '']

        if len(df_sub) < 10:
            print(f"  Skipping '{primary_cat}': only {len(df_sub)} samples")
            continue
        if df_sub['newSubCat'].nunique() < 2:
            print(f"  Skipping '{primary_cat}': only one sub-category")
            continue

        X = df_sub['generic_product_name']
        y = df_sub['newSubCat']

        # Evaluation split
        try:
            X_train, X_test, y_train, y_test = train_test_split(
                X, y, test_size=0.20, random_state=42, stratify=y
            )
        except ValueError:
            X_train, X_test, y_train, y_test = train_test_split(
                X, y, test_size=0.20, random_state=42
            )

        # Sub-models have far less data than the primary — use smaller feature count
        # to avoid over-fitting the vocabulary to training samples.
        sub_vec_kwargs = dict(stop_words='english', max_features=5_000,
                              ngram_range=(1, 2), sublinear_tf=True)

        vec_eval = TfidfVectorizer(**sub_vec_kwargs)
        vec_eval.fit(X_train)
        mdl_eval = _make_model()
        mdl_eval.fit(vec_eval.transform(X_train), y_train)
        acc = accuracy_score(y_test, mdl_eval.predict(vec_eval.transform(X_test)))

        print(f"  '{primary_cat}' — {len(df_sub):,} samples, "
              f"{y.nunique()} sub-cats, eval acc={acc:.3f}")

        # Deployment: fit on all data for this category
        vec_final = TfidfVectorizer(**sub_vec_kwargs)
        vec_final.fit(X)
        mdl_final = _make_model()
        mdl_final.fit(vec_final.transform(X), y)

        key = sanitize_filename(primary_cat)
        sub_models[key] = mdl_final
        sub_vectorizers[key] = vec_final

    return sub_models, sub_vectorizers


def sanitize_filename(name: str) -> str:
    name = re.sub(r'[^\w\-]+', '_', name)
    return name.strip('_')

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description='Retrain Nimblist classification models')
    parser.add_argument('--training-data', default=DEFAULT_TRAINING_CSV,
                        help=f'Path to combined_cleaned.csv (default: {DEFAULT_TRAINING_CSV})')
    parser.add_argument('--feedback', default=None,
                        help='Path to JSONL feedback file from GET /api/classificationfeedback/export')
    parser.add_argument('--feedback-repeat', type=int, default=5,
                        help='How many times to repeat each feedback row (default: 5)')
    parser.add_argument('--no-augmentation', action='store_true',
                        help='Disable left-truncation data augmentation')
    parser.add_argument('--augmentation-drop', type=int, default=2,
                        help='Max words to drop from the left per name (default: 2)')
    parser.add_argument('--output-dir', default=DEFAULT_OUTPUT_DIR,
                        help=f'Where to save model files (default: {DEFAULT_OUTPUT_DIR})')
    args = parser.parse_args()

    # ------------------------------------------------------------------
    # Load and merge data
    # ------------------------------------------------------------------
    if not os.path.exists(args.training_data):
        print(f'ERROR: Training data not found at {args.training_data}')
        print('Ensure combined_cleaned.csv is in scripts/ClassificationModel/')
        sys.exit(1)

    df = load_base_training_data(args.training_data)

    if args.feedback:
        if not os.path.exists(args.feedback):
            print(f'ERROR: Feedback file not found at {args.feedback}')
            sys.exit(1)
        print(f'Loading feedback from: {args.feedback}')
        fb = load_feedback(args.feedback)
        if len(fb) > 0:
            repeated = pd.concat([fb] * args.feedback_repeat, ignore_index=True)
            df = pd.concat([df, repeated], ignore_index=True)
            print(f'  Dataset after merging feedback ({args.feedback_repeat}x): {len(df):,} rows')
        else:
            print('  No usable feedback; training on base data only.')
    else:
        print('No feedback file provided; training on base data only.')

    # ------------------------------------------------------------------
    # Preprocess: apply clean_text to all names up front so augmented
    # variants are generated from already-normalised text.
    # ------------------------------------------------------------------
    print('\nApplying text preprocessing...')
    df['generic_product_name'] = df['generic_product_name'].apply(clean_text)
    # Drop any rows whose name reduced to empty string after preprocessing
    df = df[df['generic_product_name'].str.strip() != ''].reset_index(drop=True)
    print(f'  {len(df):,} rows after preprocessing')

    # ------------------------------------------------------------------
    # Augmentation
    # ------------------------------------------------------------------
    if not args.no_augmentation:
        print(f'\nAugmenting training data (max_drop={args.augmentation_drop})...')
        aug = augment_training_data(df, max_drop=args.augmentation_drop)
        df = pd.concat([df, aug], ignore_index=True)
        print(f'  {len(df):,} rows after augmentation')
    else:
        print('\nAugmentation disabled.')

    print(f'\nFinal training set: {len(df):,} rows')
    print(f'Primary categories: {sorted(df["newCat"].unique())}')

    # ------------------------------------------------------------------
    # Train
    # ------------------------------------------------------------------
    print('\n=== Training primary model ===')
    primary_model, primary_vectorizer = train_primary_model(
        df['generic_product_name'], df['newCat']
    )

    print('\n=== Training sub-category models ===')
    sub_models, sub_vectorizers = train_sub_models(df)

    # ------------------------------------------------------------------
    # Save
    # ------------------------------------------------------------------
    sub_dir = os.path.join(args.output_dir, SUB_MODELS_SUBDIR)
    os.makedirs(sub_dir, exist_ok=True)

    primary_model_path = os.path.join(args.output_dir, 'supermarket_classifier_logreg.joblib')
    primary_vec_path = os.path.join(args.output_dir, 'tfidf_vectorizer_logreg.joblib')
    print(f'\nSaving primary model     -> {primary_model_path}')
    joblib.dump(primary_model, primary_model_path)
    print(f'Saving primary vectorizer -> {primary_vec_path}')
    joblib.dump(primary_vectorizer, primary_vec_path)

    for key, mdl in sub_models.items():
        joblib.dump(mdl, os.path.join(sub_dir, f'model_sub_{key}.joblib'))
        joblib.dump(sub_vectorizers[key], os.path.join(sub_dir, f'vectorizer_sub_{key}.joblib'))
    print(f'Saved {len(sub_models)} sub-models -> {sub_dir}')

    print('\nDone. Restart the classification container to load the new models:')
    print('  docker compose restart Nimblist.classification')


if __name__ == '__main__':
    main()
