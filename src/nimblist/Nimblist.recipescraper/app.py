import base64
import json
import os
import requests
import trafilatura
from flask import Flask, request, jsonify
from recipe_scrapers import scrape_html, WebsiteNotImplementedError
from ingredient_parser import parse_ingredient

app = Flask(__name__)

# Env-var defaults — used when no per-request llm_config is supplied
_LLM_PROVIDER = os.environ.get('LLM_PROVIDER', '').lower().strip()
_LLM_MODEL = os.environ.get('LLM_MODEL', '').strip()
_LLM_VISION_MODEL = os.environ.get('LLM_VISION_MODEL', '').strip()
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


def _resolve_cfg(request_cfg=None):
    """Return an llm config dict from request body override or env var defaults."""
    if request_cfg and request_cfg.get('provider'):
        return {
            'provider': request_cfg.get('provider', '').lower().strip(),
            'model': (request_cfg.get('model') or '').strip(),
            'vision_model': (request_cfg.get('vision_model') or '').strip(),
            'api_key': (request_cfg.get('api_key') or '').strip(),
            'base_url': (request_cfg.get('base_url') or _OLLAMA_BASE_URL).rstrip('/'),
        }
    return {
        'provider': _LLM_PROVIDER,
        'model': _LLM_MODEL,
        'vision_model': _LLM_VISION_MODEL,
        'api_key': _OPENROUTER_API_KEY,
        'base_url': _OLLAMA_BASE_URL,
    }


def safe_call(fn):
    try:
        result = fn()
        return result if result is not None else None
    except Exception:
        return None


def _format_quantity(qty) -> str:
    """Format a parsed quantity value as a human-readable string.

    ingredient-parser-nlp returns quantity values as Python Fraction objects
    (e.g. Fraction(3, 2) for 1½).  str() on an improper Fraction gives "3/2",
    which is hard to read and breaks the frontend scaling parser.  Convert to
    mixed-number notation instead: Fraction(3, 2) → "1 1/2".
    """
    from fractions import Fraction
    try:
        f = Fraction(qty).limit_denominator(16)
    except (TypeError, ValueError):
        return str(qty)
    if f.denominator == 1:
        return str(f.numerator)
    whole = f.numerator // f.denominator
    remainder = f - whole
    if whole > 0:
        return f"{whole} {remainder.numerator}/{remainder.denominator}"
    return f"{remainder.numerator}/{remainder.denominator}"


def parse_ingredient_text(text):
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
                parts.append(_format_quantity(amount.quantity))
            if amount.unit:
                parts.append(str(amount.unit))
            quantity = ' '.join(parts) if parts else None
        return {"text": text, "parsed_name": name, "parsed_quantity": quantity}
    except Exception:
        return {"text": text, "parsed_name": None, "parsed_quantity": None}


# ---------------------------------------------------------------------------
# LLM helpers
# ---------------------------------------------------------------------------

def _parse_llm_json(content):
    """Strip markdown fences and parse JSON; normalise total_time to int."""
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


def _chat_openai_compat(messages, model, provider, api_key, base_url, timeout):
    """OpenAI-compatible chat completions (openrouter, openai, ollama)."""
    if provider == 'openrouter':
        url = 'https://openrouter.ai/api/v1/chat/completions'
    elif provider == 'openai':
        url = 'https://api.openai.com/v1/chat/completions'
    elif provider == 'ollama':
        url = f'{base_url}/v1/chat/completions'
        api_key = 'ollama'
    else:
        return None
    try:
        resp = requests.post(
            url,
            headers={'Authorization': f'Bearer {api_key}', 'Content-Type': 'application/json'},
            json={'model': model, 'messages': messages, 'temperature': 0.1},
            timeout=timeout,
        )
        resp.raise_for_status()
        content = resp.json()['choices'][0]['message']['content'].strip()
        return _parse_llm_json(content)
    except Exception as e:
        app.logger.warning(f"OpenAI-compat LLM call failed ({provider}/{model}): {e}")
        return None


def _to_anthropic_content(content):
    """Convert an OpenAI-style content value to Anthropic content blocks."""
    if isinstance(content, str):
        return content
    blocks = []
    for part in content:
        if part['type'] == 'text':
            blocks.append({'type': 'text', 'text': part['text']})
        elif part['type'] == 'image_url':
            url = part['image_url']['url']
            if url.startswith('data:'):
                header, data = url.split(',', 1)
                media_type = header.split(':')[1].split(';')[0]
                blocks.append({'type': 'image', 'source': {'type': 'base64', 'media_type': media_type, 'data': data}})
            else:
                blocks.append({'type': 'image', 'source': {'type': 'url', 'url': url}})
    return blocks


def _chat_anthropic(messages, model, api_key, timeout):
    """Anthropic Messages API."""
    anthropic_messages = [
        {'role': m['role'], 'content': _to_anthropic_content(m['content'])}
        for m in messages
    ]
    try:
        resp = requests.post(
            'https://api.anthropic.com/v1/messages',
            headers={
                'x-api-key': api_key,
                'anthropic-version': '2023-06-01',
                'content-type': 'application/json',
            },
            json={'model': model, 'max_tokens': 2048, 'messages': anthropic_messages},
            timeout=timeout,
        )
        resp.raise_for_status()
        content = resp.json()['content'][0]['text'].strip()
        return _parse_llm_json(content)
    except Exception as e:
        app.logger.warning(f"Anthropic call failed ({model}): {e}")
        return None


def _to_gemini_parts(content):
    """Convert an OpenAI-style content value to Gemini parts."""
    if isinstance(content, str):
        return [{'text': content}]
    parts = []
    for part in content:
        if part['type'] == 'text':
            parts.append({'text': part['text']})
        elif part['type'] == 'image_url':
            url = part['image_url']['url']
            if url.startswith('data:'):
                header, data = url.split(',', 1)
                media_type = header.split(':')[1].split(';')[0]
                parts.append({'inlineData': {'mimeType': media_type, 'data': data}})
            else:
                # Gemini doesn't accept arbitrary URLs — fetch and inline
                try:
                    img_resp = requests.get(url, timeout=15)
                    img_resp.raise_for_status()
                    b64 = base64.b64encode(img_resp.content).decode()
                    media_type = img_resp.headers.get('content-type', 'image/jpeg').split(';')[0]
                    parts.append({'inlineData': {'mimeType': media_type, 'data': b64}})
                except Exception as e:
                    app.logger.warning(f"Failed to fetch image for Gemini: {e}")
    return parts


def _chat_gemini(messages, model, api_key, timeout):
    """Google Gemini generateContent API."""
    contents = [
        {'role': 'user' if m['role'] == 'user' else 'model',
         'parts': _to_gemini_parts(m['content'])}
        for m in messages
    ]
    url = f'https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}'
    try:
        resp = requests.post(
            url,
            headers={'Content-Type': 'application/json'},
            json={'contents': contents},
            timeout=timeout,
        )
        resp.raise_for_status()
        text = resp.json()['candidates'][0]['content']['parts'][0]['text'].strip()
        return _parse_llm_json(text)
    except Exception as e:
        app.logger.warning(f"Gemini call failed ({model}): {e}")
        return None


def _llm_chat(messages, cfg, timeout=30):
    """Dispatch to the right provider."""
    provider = cfg['provider']
    model = cfg['model']
    if not provider or not model:
        return None
    if provider == 'anthropic':
        return _chat_anthropic(messages, model, cfg['api_key'], timeout)
    if provider == 'gemini':
        return _chat_gemini(messages, model, cfg['api_key'], timeout)
    return _chat_openai_compat(messages, model, provider, cfg['api_key'], cfg['base_url'], timeout)


def _llm_extract_recipe(page_html, cfg):
    page_text = trafilatura.extract(page_html, include_comments=False, include_tables=True)
    if not page_text:
        return None
    messages = [{'role': 'user', 'content': _LLM_RECIPE_PROMPT + '\nText:\n' + page_text[:6000]}]
    return _llm_chat(messages, cfg)


def _llm_extract_from_image(image_source, cfg):
    model = cfg['vision_model'] or cfg['model']
    vision_cfg = {**cfg, 'model': model}
    messages = [{
        'role': 'user',
        'content': [
            {'type': 'image_url', 'image_url': {'url': image_source}},
            {'type': 'text', 'text': _LLM_RECIPE_PROMPT},
        ],
    }]
    return _llm_chat(messages, vision_cfg, timeout=60)


# ---------------------------------------------------------------------------
# Scraping helpers
# ---------------------------------------------------------------------------

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
    try:
        return _create_scraper(html, url), None
    except Exception as e:
        return None, e


def _scraper_result(scraper):
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


def _extract_og_image(html):
    from bs4 import BeautifulSoup
    soup = BeautifulSoup(html, 'html.parser')
    for selector in [{'property': 'og:image'}, {'name': 'twitter:image'}, {'name': 'twitter:image:src'}]:
        tag = soup.find('meta', attrs=selector)
        if tag and tag.get('content'):
            return tag['content']
    return None


def _llm_result(llm, scraper, og_image=None):
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

    cfg = _resolve_cfg(data.get('llm_config'))

    resp, fetch_err = _fetch_page(url)
    if fetch_err:
        return jsonify({"error": fetch_err[0]}), fetch_err[1]

    scraper, scraper_err = _try_scraper(resp.text, url)
    if scraper_err and not cfg['provider']:
        return jsonify({"error": f"Could not find recipe data on this page: {scraper_err}"}), 422

    raw_ingredients, result = _scraper_result(scraper) if scraper else ([], None)

    if not raw_ingredients and cfg['provider']:
        app.logger.info(f"Falling back to LLM extraction for {url}")
        llm = _llm_extract_recipe(resp.text, cfg)
        if llm:
            og_image = _extract_og_image(resp.text)
            return jsonify(_llm_result(llm, scraper, og_image=og_image))
        if not scraper:
            return jsonify({"error": "Could not find recipe data on this page"}), 422

    return jsonify(result)


@app.route('/scrape-image', methods=['POST'])
def scrape_image():
    data = request.get_json(silent=True)
    if not data:
        return jsonify({"error": "Missing request body"}), 400

    cfg = _resolve_cfg(data.get('llm_config'))
    if not cfg['provider']:
        return jsonify({"error": "LLM provider is not configured"}), 503

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

    result = _llm_extract_from_image(image_source, cfg)
    if not result:
        return jsonify({"error": "Could not extract recipe from image"}), 422

    return jsonify(_llm_result(result, None))


if __name__ == '__main__':
    app.run(debug=True, host=os.environ.get('FLASK_HOST', '127.0.0.1'), port=5001)
