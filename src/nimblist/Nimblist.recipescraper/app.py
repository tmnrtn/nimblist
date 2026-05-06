import requests
from flask import Flask, request, jsonify
from recipe_scrapers import scrape_html, WebsiteNotImplementedError
from ingredient_parser import parse_ingredient

app = Flask(__name__)


def safe_call(fn):
    try:
        result = fn()
        return result if result is not None else None
    except Exception:
        return None


def parse_ingredient_text(text):
    """Parse a raw ingredient string into {text, parsed_name, parsed_quantity}.
    Falls back gracefully if parsing fails or produces low-confidence results."""
    try:
        result = parse_ingredient(text)

        name = None
        if result.name:
            name_parts = [n.text for n in result.name if n.text]
            name = ' '.join(name_parts) if name_parts else None

        quantity = None
        if result.amount:
            amount = result.amount[0]
            parts = []
            if amount.quantity:
                parts.append(str(amount.quantity))
            if amount.unit:
                parts.append(str(amount.unit))
            quantity = ' '.join(parts) if parts else None

        return {"text": text, "parsed_name": name, "parsed_quantity": quantity}
    except Exception:
        return {"text": text, "parsed_name": None, "parsed_quantity": None}


@app.route('/health', methods=['GET'])
def health():
    return jsonify({"status": "ok"})


@app.route('/parse-ingredients', methods=['POST'])
def parse_ingredients_endpoint():
    data = request.get_json(silent=True)
    if not data or 'ingredients' not in data:
        return jsonify({"error": "Missing 'ingredients' in request body"}), 400
    texts = data['ingredients']
    if not isinstance(texts, list):
        return jsonify({"error": "'ingredients' must be a list"}), 400
    return jsonify([parse_ingredient_text(str(t)) for t in texts])


def _create_scraper(html, url):
    try:
        return scrape_html(html, org_url=url)
    except WebsiteNotImplementedError:
        return scrape_html(html, org_url=url, supported_only=False)


def _extract_instructions(scraper):
    if hasattr(scraper, 'instructions_list'):
        steps = safe_call(scraper.instructions_list)
        if steps and isinstance(steps, list) and len(steps) > 0:
            return '\n'.join(steps)
    return safe_call(scraper.instructions)


@app.route('/scrape', methods=['POST'])
def scrape():
    data = request.get_json(silent=True)
    if not data or 'url' not in data:
        return jsonify({"error": "Missing 'url' in request body"}), 400

    url = data['url'].strip()
    if not url.startswith(('http://', 'https://')):
        return jsonify({"error": "Invalid URL — must start with http:// or https://"}), 400

    try:
        resp = requests.get(
            url,
            timeout=15,
            headers={'User-Agent': 'Mozilla/5.0 (compatible; Nimblist/1.0 recipe importer)'},
            allow_redirects=True,
        )
        resp.raise_for_status()
    except requests.Timeout:
        return jsonify({"error": "Request timed out fetching the URL"}), 422
    except requests.RequestException as e:
        return jsonify({"error": f"Failed to fetch URL: {str(e)}"}), 422

    try:
        scraper = _create_scraper(resp.text, url)
    except Exception as e:
        return jsonify({"error": f"Could not find recipe data on this page: {str(e)}"}), 422

    raw_ingredients = safe_call(scraper.ingredients) or []
    parsed_ingredients = [parse_ingredient_text(ing) for ing in raw_ingredients]

    return jsonify({
        "title": safe_call(scraper.title) or "Untitled Recipe",
        "description": safe_call(scraper.description),
        "image": safe_call(scraper.image),
        "yields": safe_call(scraper.yields),
        "total_time": safe_call(scraper.total_time),
        "ingredients": parsed_ingredients,
        "instructions": _extract_instructions(scraper),
    })


if __name__ == '__main__':
    import os
    app.run(debug=True, host=os.environ.get('FLASK_HOST', '127.0.0.1'), port=5001)
