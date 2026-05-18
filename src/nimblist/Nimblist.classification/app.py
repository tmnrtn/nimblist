import os
import re
import joblib
from flask import Flask, request, jsonify
import numpy as np # Import numpy

# --- Configuration ---
PRIMARY_MODEL_PATH = 'supermarket_classifier_logreg.joblib'
PRIMARY_VECTORIZER_PATH = 'tfidf_vectorizer_logreg.joblib'
SUB_MODELS_DIR = 'sub_category_models'

# Return "Unknown" when the model's top probability is below this threshold.
# Prevents confidently-wrong classifications for short/ambiguous inputs.
# Override with PRIMARY_CONFIDENCE_THRESHOLD / SUB_CONFIDENCE_THRESHOLD env vars.
PRIMARY_CONFIDENCE_THRESHOLD = float(os.environ.get('PRIMARY_CONFIDENCE_THRESHOLD', '0.35'))
SUB_CONFIDENCE_THRESHOLD = float(os.environ.get('SUB_CONFIDENCE_THRESHOLD', '0.35'))

# --- Load Models and Vectorizers ---
print("Loading primary model and vectorizer...")
try:
    primary_model = joblib.load(PRIMARY_MODEL_PATH)
    primary_vectorizer = joblib.load(PRIMARY_VECTORIZER_PATH)
    print("Primary model and vectorizer loaded successfully.")
except FileNotFoundError:
    print(f"Error: Primary model or vectorizer not found at expected paths.")
    primary_model = None
    primary_vectorizer = None
except Exception as e:
    print(f"Error loading primary model/vectorizer: {e}")
    primary_model = None
    primary_vectorizer = None


print("Loading sub-category models and vectorizers...")
sub_models = {}
sub_vectorizers = {}
if os.path.exists(SUB_MODELS_DIR):
    for filename in os.listdir(SUB_MODELS_DIR):
        try:
            # Extract category name and type (model/vectorizer) from filename
            parts = filename.replace('.joblib', '').split('_sub_')
            if len(parts) == 2:
                type_prefix = parts[0] # Should be 'model' or 'vectorizer'
                category_name_sanitized = parts[1] # This is the sanitized name used during saving

                # Need a way to map sanitized name back to original if needed,
                # or rely on the prediction output of primary model matching sanitized names.
                # For simplicity, assume primary model predicts original names
                # which we might need to sanitize OR load based on sanitized names.
                # Let's load using the sanitized name as the key for now.

                full_path = os.path.join(SUB_MODELS_DIR, filename)

                if type_prefix == 'model':
                    sub_models[category_name_sanitized] = joblib.load(full_path)
                    # print(f"Loaded sub-model for: {category_name_sanitized}")
                elif type_prefix == 'vectorizer':
                    sub_vectorizers[category_name_sanitized] = joblib.load(full_path)
                    # print(f"Loaded sub-vectorizer for: {category_name_sanitized}")

        except Exception as e:
            print(f"Error loading file {filename}: {e}")
    print(f"Loaded {len(sub_models)} sub-models and {len(sub_vectorizers)} sub-vectorizers.")
else:
    print(f"Warning: Sub-models directory '{SUB_MODELS_DIR}' not found.")


# --- Text cleaning (MUST stay identical to retrain.py) ---

def _lemmatize_word(word: str) -> str:
    """Rule-based lemmatization for common grocery plural forms. No external dependencies."""
    if len(word) <= 2:
        return word
    # ies → y: berries→berry, pastries→pastry, strawberries→strawberry
    if len(word) > 4 and word.endswith('ies'):
        return word[:-3] + 'y'
    # ves → f: loaves→loaf, halves→half
    if len(word) > 4 and word.endswith('ves'):
        return word[:-3] + 'f'
    # oes → o: tomatoes→tomato, potatoes→potato, mangoes→mango
    if len(word) > 5 and word.endswith('oes'):
        return word[:-2]
    # Standard plural s: eggs→egg, biscuits→biscuit, carrots→carrot
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
    Applied identically at training time (retrain.py) and inference time (here).
    """
    text = str(text).lower()
    # Strip size/quantity patterns before removing punctuation so word
    # boundaries still work against digit+unit tokens.
    text = re.sub(r'\b\d+(\.\d+)?\s*(g|kg|ml|l|lb|oz|cl|mg|litre|litres|liter|liters|pint|pints)\b',
                  ' ', text, flags=re.IGNORECASE)
    text = re.sub(r'\b\d+\s*x\s*\d+(\.\d+)?\s*(g|kg|ml|l|lb|oz|cl|mg)?\b', ' ', text)  # 4 x 500g
    text = re.sub(r'\bx\d+\b', ' ', text)                          # x6
    text = re.sub(r'\b\d+\s*pack\b', ' ', text, flags=re.IGNORECASE)         # 6 pack
    text = re.sub(r'\bpack\s+of\s+\d+\b', ' ', text, flags=re.IGNORECASE)    # pack of 12
    text = re.sub(r'[^\w\s]', '', text)           # remove punctuation
    text = re.sub(r'\s+', ' ', text).strip()      # normalise whitespace
    text = ' '.join(_lemmatize_word(w) for w in text.split())
    return text

# --- Filename Sanitization (MUST match saving script) ---
def sanitize_filename(name):
    name = re.sub(r'[^\w\-]+', '_', name)
    name = name.strip('_')
    return name

# --- Flask App ---
app = Flask(__name__)

@app.route('/health', methods=['GET'])
def health_check():
    return jsonify({"status": "ok"})

def _predict_sub_category(primary_cat, input_vector):
    sanitized = sanitize_filename(primary_cat)
    if sanitized not in sub_models or sanitized not in sub_vectorizers:
        print(f"Warning: No sub-model found for '{primary_cat}' (Sanitized: '{sanitized}').")
        return None
    try:
        sub_features = sub_vectorizers[sanitized].transform(input_vector)
        proba = sub_models[sanitized].predict_proba(sub_features)[0]
        max_confidence = float(np.max(proba))
        if max_confidence < SUB_CONFIDENCE_THRESHOLD:
            return None
        return sub_models[sanitized].classes_[int(np.argmax(proba))]
    except Exception as e:
        print(f"Error during sub-category prediction for '{primary_cat}': {e}")
        return None


@app.route('/predict', methods=['POST'])
def predict():
    if not primary_model or not primary_vectorizer:
        return jsonify({"error": "Models not loaded properly"}), 500

    try:
        data = request.get_json()
        if not data:
            return jsonify({"error": "Request must contain valid JSON"}), 400
        if 'product_name' not in data:
            return jsonify({"error": "Missing 'product_name' in JSON payload"}), 400
    except Exception:
        return jsonify({"error": "Invalid JSON format"}), 400

    product_name = data['product_name']
    cleaned_name = clean_text(product_name)
    input_vector = [cleaned_name]

    try:
        primary_features = primary_vectorizer.transform(input_vector)
        proba = primary_model.predict_proba(primary_features)[0]
        max_confidence = float(np.max(proba))
        if max_confidence < PRIMARY_CONFIDENCE_THRESHOLD:
            predicted_primary_cat = None
        else:
            predicted_primary_cat = primary_model.classes_[int(np.argmax(proba))]
    except Exception as e:
        print(f"Error during primary prediction: {e}")
        return jsonify({"error": "Failed to predict primary category"}), 500

    predicted_sub_cat = None
    if predicted_primary_cat is not None:
        predicted_sub_cat = _predict_sub_category(predicted_primary_cat, input_vector)

    return jsonify({
        "input_product_name": product_name,
        "cleaned_product_name": cleaned_name,
        "predicted_primary_category": predicted_primary_cat,
        "predicted_sub_category": predicted_sub_cat
    })

# Run directly for development (python app.py)
# Use Gunicorn for production (see Dockerfile CMD)
if __name__ == '__main__':
    import os
    app.run(debug=True, host=os.environ.get('FLASK_HOST', '127.0.0.1'), port=5000)
