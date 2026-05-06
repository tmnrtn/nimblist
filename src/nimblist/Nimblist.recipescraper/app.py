import json
import os
import requests
import trafilatura
from flask import Flask, request, jsonify
from recipe_scrapers import scrape_html, WebsiteNotImplementedError
from ingredient_parser import parse_ingredient

app = Flask(__name__)

# LLM config — set LLM_PROVIDER to 'openrouter' or 'ollama' to enable
_LLM_PROVIDER = os.environ.get('LLM_PROVIDER', '').lower().strip()
_LLM_MODEL = os.environ.get('LLM_MODEL', '').strip()
_LLM_VISION_MODEL = os.environ.get('LLM_VISION_MODEL', '').strip()  # falls back to _LLM_MODEL if unset
_OPENROUTER_API_KEY = os.environ.get('OPENROUTER_API_KEY', '').strip()
_OLLAMA_BASE_URL = os.environ.get('OLLAMA_BASE_URL', 'http://localhost:11434').rstrip('/')

_LLM_RECIPE_PROMPT = """\
Extract the recipe and return ONLY a JSON object with these exact fields (no markdown, no explanation):
{
  "title": "string",
  "description": "string or null",
  "yields": "string or null (e.g. '4 servings')",
  "total_time": integer or null (total minutes as a number),
  "ingredients": ["list of raw ingredient strings"],
  "instructions": "string with steps separated by newlines"
}
"""


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


def _llm_api_url_and_auth():
    """Return (api_url, auth_header) for the configured provider, or (None, None)."""
    if _LLM_PROVIDER == 'openrouter':
        return 'https://openrouter.ai/api/v1/chat/completions', f'Bearer {_OPENROUTER_API_KEY}'
    if _LLM_PROVIDER == 'ollama':
        return f'{_OLLAMA_BASE_URL}/v1/chat/completions', 'Bearer ollama'
    app.logger.warning(f"Unknown LLM_PROVIDER '{_LLM_PROVIDER}'")
    return None, None


def _llm_chat(messages: list, model: str, timeout: int = 30) -> dict | None:
    """Send a chat completion to the configured provider; return parsed JSON or None."""
    if not _LLM_PROVIDER or not model:
        return None
    api_url, auth_header = _llm_api_url_and_auth()
    if not api_url:
        return None
    try:
        resp = requests.post(
            api_url,
            headers={'Authorization': auth_header, 'Content-Type': 'application/json'},
            json={'model': model, 'messages': messages, 'temperature': 0.1},
            timeout=timeout,
        )
        resp.raise_for_status()
        content = resp.json()['choices'][0]['message']['content'].strip()
        if content.startswith('```'):
            lines = content.splitlines()
            content = '\n'.join(lines[1:-1] if lines[-1].strip() == '```' else lines[1:])
        data = json.loads(content)
        tt = data.get('total_time')
        if isinstance(tt, str):
            try:
                data['total_time'] = int(tt)
            except ValueError:
                data['total_time'] = None
        return data
    except Exception as e:
        app.logger.warning(f"LLM call failed ({model}): {e}")
        return None


def _llm_extract_recipe(page_html: str) -> dict | None:
    """LLM fallback for text pages — extracts clean text via trafilatura first."""
    page_text = trafilatura.extract(page_html, include_comments=False, include_tables=True)
    if not page_text:
        return None
    messages = [{'role': 'user', 'content': _LLM_RECIPE_PROMPT + '\nText:\n' + page_text[:6000]}]
    return _llm_chat(messages, _LLM_MODEL)


def _llm_extract_from_image(image_source: str) -> dict | None:
    """Extract recipe from an image using the configured vision model."""
    model = _LLM_VISION_MODEL or _LLM_MODEL
    messages = [{
        'role': 'user',
        'content': [
            {'type': 'image_url', 'image_url': {'url': image_source}},
            {'type': 'text', 'text': _LLM_RECIPE_PROMPT},
        ],
    }]
    return _llm_chat(messages, model, timeout=60)


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


def _fetch_page(url):
    """Return (response, None) or (None, (message, status)) on failure."""
    try:
        resp = requests.get(
            url,
            timeout=15,
            headers={'User-Agent': 'Mozilla/5.0 (compatible; Nimblist/1.0 recipe importer)'},
            allow_redirects=True,
        )
        resp.raise_for_status()
        return resp, None
    except requests.Timeout:
        return None, ("Request timed out fetching the URL", 422)
    except requests.RequestException as e:
        return None, (f"Failed to fetch URL: {str(e)}", 422)


def _try_scraper(html, url):
    """Return (scraper, None) or (None, exception) on failure."""
    try:
        return _create_scraper(html, url), None
    except Exception as e:
        return None, e


def _scraper_result(scraper):
    """Return (raw_ingredients, result_dict) from a live scraper."""
    raw = safe_call(scraper.ingredients) or []
    return raw, {
        "title": safe_call(scraper.title) or "Untitled Recipe",
        "description": safe_call(scraper.description),
        "image": safe_call(scraper.image),
        "yields": safe_call(scraper.yields),
        "total_time": safe_call(scraper.total_time),
        "ingredients": [parse_ingredient_text(ing) for ing in raw],
        "instructions": _extract_instructions(scraper),
    }


def _extract_og_image(html: str) -> str | None:
    """Extract og:image or twitter:image from page HTML as a fallback image source."""
    from bs4 import BeautifulSoup
    soup = BeautifulSoup(html, 'html.parser')
    for selector in [{'property': 'og:image'}, {'name': 'twitter:image'}, {'name': 'twitter:image:src'}]:
        tag = soup.find('meta', attrs=selector)
        if tag and tag.get('content'):
            return tag['content']
    return None


def _llm_result(llm, scraper, og_image: str | None = None):
    """Merge LLM-extracted data with any scraper fields available."""
    sc = scraper
    return {
        "title": llm.get('title') or (safe_call(sc.title) if sc else None) or "Untitled Recipe",
        "description": llm.get('description') or (safe_call(sc.description) if sc else None),
        "image": (safe_call(sc.image) if sc else None) or og_image,
        "yields": llm.get('yields') or (safe_call(sc.yields) if sc else None),
        "total_time": llm.get('total_time') or (safe_call(sc.total_time) if sc else None),
        "ingredients": [parse_ingredient_text(str(i)) for i in (llm.get('ingredients') or [])],
        "instructions": llm.get('instructions') or (_extract_instructions(sc) if sc else None),
    }


@app.route('/scrape', methods=['POST'])
def scrape():
    data = request.get_json(silent=True)
    if not data or 'url' not in data:
        return jsonify({"error": "Missing 'url' in request body"}), 400

    url = data['url'].strip()
    if not url.startswith(('http://', 'https://')):
        return jsonify({"error": "Invalid URL — must start with http:// or https://"}), 400

    resp, fetch_err = _fetch_page(url)
    if fetch_err:
        return jsonify({"error": fetch_err[0]}), fetch_err[1]

    scraper, scraper_err = _try_scraper(resp.text, url)
    if scraper_err and not _LLM_PROVIDER:
        return jsonify({"error": f"Could not find recipe data on this page: {scraper_err}"}), 422

    raw_ingredients, result = _scraper_result(scraper) if scraper else ([], None)

    if not raw_ingredients and _LLM_PROVIDER:
        app.logger.info(f"Falling back to LLM extraction for {url}")
        llm = _llm_extract_recipe(resp.text)
        if llm:
            og_image = _extract_og_image(resp.text)
            return jsonify(_llm_result(llm, scraper, og_image=og_image))
        if not scraper:
            return jsonify({"error": "Could not find recipe data on this page"}), 422

    return jsonify(result)


@app.route('/scrape-image', methods=['POST'])
def scrape_image():
    if not _LLM_PROVIDER:
        return jsonify({"error": "LLM_PROVIDER is not configured"}), 503

    data = request.get_json(silent=True)
    if not data:
        return jsonify({"error": "Missing request body"}), 400

    if 'image_url' in data:
        image_source = data['image_url'].strip()
        if not image_source.startswith(('http://', 'https://')):
            return jsonify({"error": "image_url must start with http:// or https://"}), 400
    elif 'image' in data:
        image_b64 = data['image'].strip()
        if image_b64.startswith('data:'):
            image_source = image_b64
        else:
            media_type = data.get('media_type', 'image/jpeg')
            image_source = f"data:{media_type};base64,{image_b64}"
    else:
        return jsonify({"error": "Provide either 'image_url' or 'image' in the request body"}), 400

    result = _llm_extract_from_image(image_source)
    if not result:
        return jsonify({"error": "Could not extract recipe from image"}), 422

    return jsonify(_llm_result(result, None))


if __name__ == '__main__':
    app.run(debug=True, host=os.environ.get('FLASK_HOST', '127.0.0.1'), port=5001)
