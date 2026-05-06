FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

# lxml (pulled in by extruct/recipe-scrapers) needs these
RUN apt-get update && apt-get install -y --no-install-recommends \
    libxml2-dev \
    libxslt1-dev \
    && rm -rf /var/lib/apt/lists/*

COPY src/nimblist/Nimblist.recipescraper/requirements.lock .
RUN pip install --no-cache-dir --require-hashes -r requirements.lock

COPY src/nimblist/Nimblist.recipescraper/app.py .

EXPOSE 5001

CMD ["gunicorn", "--bind", "0.0.0.0:5001", "--workers", "2", "--timeout", "60", "app:app"]
