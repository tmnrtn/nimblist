# Use an official Python runtime as a parent image
FROM python:3.9-slim

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

# Set the working directory in the container
WORKDIR /app

# Install system dependencies needed for building Python packages with C extensions
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    gcc \
    && rm -rf /var/lib/apt/lists/*

# Copy the requirements file first to leverage Docker cache
COPY src/nimblist/Nimblist.classification/requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

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