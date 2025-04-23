import os
import re
import joblib
from flask import Flask, request, jsonify
import numpy as np # Import numpy

# --- Configuration ---
PRIMARY_MODEL_PATH = 'supermarket_classifier_logreg.joblib'
PRIMARY_VECTORIZER_PATH = 'tfidf_vectorizer_logreg.joblib'
SUB_MODELS_DIR = 'sub_category_models'

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


# --- Text Cleaning Function (MUST match training) ---
def clean_text(text):
    if not isinstance(text, str):
         text = str(text) # Ensure input is string
    text = text.lower() # Lowercase
    text = re.sub(r'[^\w\s]', '', text) # Remove punctuation
    text = re.sub(r'\s+', ' ', text).strip() # Remove extra whitespace
    return text

# --- Filename Sanitization (MUST match saving script) ---
def sanitize_filename(name):
    name = re.sub(r'[^\w\-]+', '_', name)
    name = name.strip('_')
    return name

# --- Flask App ---
app = Flask(__name__)

@app.route('/predict', methods=['POST'])
def predict():
    if not primary_model or not primary_vectorizer:
         return jsonify({"error": "Models not loaded properly"}), 500

    data = request.get_json()
    if not data or 'product_name' not in data:
        return jsonify({"error": "Missing 'product_name' in JSON payload"}), 400

    product_name = data['product_name']

    # 1. Clean input text
    cleaned_name = clean_text(product_name)
    input_vector = [cleaned_name] # Vectorizer expects iterable

    # 2. Predict Primary Category
    try:
        primary_features = primary_vectorizer.transform(input_vector)
        predicted_primary_cat_arr = primary_model.predict(primary_features)
        predicted_primary_cat = predicted_primary_cat_arr[0] if len(predicted_primary_cat_arr) > 0 else "Unknown"
    except Exception as e:
        print(f"Error during primary prediction: {e}")
        return jsonify({"error": "Failed to predict primary category"}), 500


    predicted_sub_cat = "N/A" # Default if sub-model not found or error occurs

    # 3. Predict Sub-Category (if primary prediction successful)
    if predicted_primary_cat != "Unknown":
        # Sanitize the *predicted* primary category name to match the keys used for loading
        sanitized_primary_cat = sanitize_filename(predicted_primary_cat)

        if sanitized_primary_cat in sub_models and sanitized_primary_cat in sub_vectorizers:
            try:
                sub_vectorizer = sub_vectorizers[sanitized_primary_cat]
                sub_model = sub_models[sanitized_primary_cat]

                sub_features = sub_vectorizer.transform(input_vector)
                predicted_sub_cat_arr = sub_model.predict(sub_features)
                predicted_sub_cat = predicted_sub_cat_arr[0] if len(predicted_sub_cat_arr) > 0 else "Unknown"

            except Exception as e:
                print(f"Error during sub-category prediction for '{predicted_primary_cat}': {e}")
                predicted_sub_cat = "Prediction Error"
        else:
            print(f"Warning: No sub-model found for predicted primary category '{predicted_primary_cat}' (Sanitized: '{sanitized_primary_cat}').")
            predicted_sub_cat = "No Sub-Model"


    # 4. Return results
    result = {
        "input_product_name": product_name,
        "cleaned_product_name": cleaned_name,
        "predicted_primary_category": predicted_primary_cat,
        "predicted_sub_category": predicted_sub_cat
    }
    return jsonify(result)

@app.route('/health', methods=['GET'])
def health_check():
    # Basic health check endpoint
    # Can be expanded to check model loading status
    status = "OK" if primary_model and primary_vectorizer else "Error: Models not loaded"
    return jsonify({"status": status})


# Run directly for development (python app.py)
# Use Gunicorn for production (see Dockerfile CMD)
if __name__ == '__main__':
    # Use host='0.0.0.0' to be accessible outside the container if running locally without Gunicorn
    app.run(debug=True, host='0.0.0.0', port=5000)