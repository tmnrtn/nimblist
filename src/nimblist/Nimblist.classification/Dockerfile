# Use an official Python runtime as a parent image
FROM python:3.11-slim

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

# Set the working directory in the container
WORKDIR /app

# Copy the pinned lock file first to leverage Docker cache
COPY src/nimblist/Nimblist.classification/requirements.lock .

# Install Python dependencies from pinned versions using binary wheels only
RUN pip install --no-cache-dir --only-binary :all: --require-hashes -r requirements.lock

# Copy the rest of the application code and models
COPY src/nimblist/Nimblist.classification/app.py .
COPY src/nimblist/Nimblist.classification/supermarket_classifier_logreg.joblib .
COPY src/nimblist/Nimblist.classification/tfidf_vectorizer_logreg.joblib .
COPY src/nimblist/Nimblist.classification/sub_category_models/ ./sub_category_models/

# Make port 5000 available to the world outside this container
EXPOSE 5000

# Run the application using Gunicorn
# Increase workers based on your server's CPU cores (e.g., (2 * cores) + 1)
# Increase timeout if model loading or predictions take longer
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "2", "--timeout", "120", "app:app"]