import asyncio
from playwright.async_api import async_playwright, TimeoutError as PlaywrightTimeoutError
import csv
import time
import re
import random

# Target URL for Tesco groceries
URL = "https://www.tesco.com/groceries/en-GB"

# --- IMPORTANT: Selectors ---
# Cookie consent selector
COOKIE_CONSENT_SELECTOR = 'button.ddsweb-consent-banner__button'

# Main category selectors
CATEGORY_CONTAINER_SELECTOR = 'ul.IDrlr'
CATEGORY_ITEM_SELECTOR = 'ul.IDrlr li.esFBEz a'

# Subcategory selectors
SUBCATEGORY_CONTAINER_SELECTOR = 'ul.pE1Is'
SUBCATEGORY_ITEM_SELECTOR = 'ul.pE1Is li a.ddsweb-categoryPill__link'

# Product selectors
PRODUCT_LIST_SELECTOR = 'ul.list-content'
PRODUCT_ITEM_SELECTOR = 'li.WL_DZ'
NEXT_PAGE_BUTTON_SELECTOR = 'a[data-testid="next"]'

async def scrape_products(page, subcategory_url, subcategory_name, parent_category):
    """
    Scrapes products from a subcategory page.
    
    Args:
        page: The Playwright page object
        subcategory_url (str): The URL of the subcategory page
        subcategory_name (str): The name of the subcategory
        parent_category (str): The name of the parent category
        
    Returns:
        list: A list of dictionaries containing product information
    """
    products = []
    
    try:
        filtered_subcategory_url = subcategory_url + "?productSource=Ghs&count=48"
        print(f"\nNavigating to subcategory: {subcategory_name} ({subcategory_url})")
        await page.goto(filtered_subcategory_url, wait_until='domcontentloaded', timeout=60000)
        print(f"Page loaded for {subcategory_name}")
        
        # Add human-like delay
        await page.wait_for_timeout(random.randint(1000, 2500))
        
        # Wait for product list container to appear
        print(f"Waiting for product list to load")
        try:
            # Wait for products to load - using li elements with WL_DZ class
            await page.wait_for_selector('li.WL_DZ', timeout=30000)
            print("Product list found.")
        except PlaywrightTimeoutError:
            print(f"No products found for '{subcategory_name}'. Selector may be incorrect or page structure differs.")
            return []
        
        # Process each page of products
        page_num = 1
        max_pages = 10  # Limit to 10 pages to avoid detection
        
        while page_num <= max_pages:
            print(f"Processing page {page_num}...")
            
            # Add random delay for human-like behavior
            await page.wait_for_timeout(random.randint(800, 1500))
            
            # Extract products from current page - using correct selector
            product_elements = page.locator('li.WL_DZ')
            count = await product_elements.count()
            print(f"Found {count} products on page {page_num}.")
            
            # Process each product on the current page
            for i in range(count):
                try:
                    # Add slight delay between products
                    if i > 0 and i % 3 == 0:
                        await page.wait_for_timeout(random.randint(500, 1000))
                    
                    element = product_elements.nth(i)
                    
                    # Extract product ID from data-testid attribute
                    product_id = await element.get_attribute("data-testid") or ""
                    
                    # Extract product name (now from a span element inside the product title link)
                    title_span = element.locator('.ddsweb-title-link__link .ddsweb-link__text')
                    product_name = await title_span.inner_text() if await title_span.count() > 0 else "Name not available"
                    
                    # Extract standard price
                    price_selector = element.locator('.ddsweb-price__container .styled__PriceText-sc-v0qv7n-1')
                    price_text = await price_selector.inner_text() if await price_selector.count() > 0 else ""
                    
                    # Extract price per unit
                    price_per_unit_selector = element.locator('.ddsweb-price__container .ddsweb-price__subtext')
                    price_per_unit = await price_per_unit_selector.inner_text() if await price_per_unit_selector.count() > 0 else ""
                    
                    # Extract Clubcard price if available
                    clubcard_price_selector = element.locator('.ddsweb-value-bar__content-text')
                    clubcard_price = await clubcard_price_selector.inner_text() if await clubcard_price_selector.count() > 0 else ""
                    
                    # Extract Clubcard price per unit if available
                    clubcard_price_per_unit_selector = element.locator('.ddsweb-value-bar__content-subtext')
                    clubcard_price_per_unit = await clubcard_price_per_unit_selector.inner_text() if await clubcard_price_per_unit_selector.count() > 0 else ""
                    
                    # Extract customer rating if available
                    rating_selector = element.locator('.ddsweb-rating__container[aria-label*="rating"]')
                    rating = ""
                    if await rating_selector.count() > 0:
                        rating_text = await rating_selector.get_attribute('aria-label')
                        if rating_text:
                            rating_match = re.search(r'(\d+\.\d+) out of 5 stars', rating_text)
                            if rating_match:
                                rating = rating_match.group(1)
                    
                    # Extract number of reviews if available
                    reviews_selector = element.locator('.ddsweb-rating__hint')
                    reviews_count = ""
                    if await reviews_selector.count() > 0:
                        reviews_text = await reviews_selector.inner_text()
                        if reviews_text:
                            reviews_match = re.search(r'\(([\d,]+)\)', reviews_text)
                            if reviews_match:
                                reviews_count = reviews_match.group(1)
                    
                    # Extract product URL
                    url_selector = element.locator('.ddsweb-title-link__link')
                    product_href = await url_selector.get_attribute('href') if await url_selector.count() > 0 else ""
                    product_url = ""
                    if product_href:
                        if product_href.startswith('/'):
                            base_url = "https://www.tesco.com"
                            product_url = f"{base_url}{product_href}"
                        else:
                            product_url = product_href
                    
                    # Extract product image URL
                    img_selector = element.locator('img[data-testid^="imageElement_"]')
                    img_url = await img_selector.get_attribute('src') if await img_selector.count() > 0 else ""
                    
                    # Check if product is sponsored
                    sponsored_selector = element.locator('.ddsweb-tag__container')
                    is_sponsored = False
                    if await sponsored_selector.count() > 0:
                        sponsored_text = await sponsored_selector.inner_text()
                        is_sponsored = "Sponsored" in sponsored_text
                    
                    # Check if product has a promotion offer (excluding Clubcard)
                    promo_selector = element.locator('.styled__PromotionsContainer-sc-nc07d4-3')
                    has_promotion = await promo_selector.count() > 0
                    
                    # Extract offer validity dates if Clubcard price is available
                    offer_dates = ""
                    offer_dates_selector = element.locator('.styled__TermsText-sc-1d7lp92-10')
                    if await offer_dates_selector.count() > 0:
                        offer_dates = await offer_dates_selector.inner_text()
                    
                    # Create product data object
                    product_data = {
                        'product_id': product_id,
                        'product_name': product_name,
                        'standard_price': price_text,
                        'price_per_unit': price_per_unit,
                        'clubcard_price': clubcard_price,
                        'clubcard_price_per_unit': clubcard_price_per_unit,
                        'has_clubcard_offer': bool(clubcard_price),
                        'offer_dates': offer_dates,
                        'other_promotion': has_promotion and not bool(clubcard_price),
                        'rating': rating,
                        'review_count': reviews_count,
                        'is_sponsored': is_sponsored,
                        'product_url': product_url,
                        'image_url': img_url,
                        'parent_category': parent_category,
                        'subcategory': subcategory_name
                    }
                    products.append(product_data)
                    
                    # Print basic info for logging
                    price_info = f"{price_text}"
                    if clubcard_price:
                        price_info += f" (Clubcard: {clubcard_price})"
                    print(f"  Extracted: {product_name[:40]}{'...' if len(product_name) > 40 else ''} - {price_info}")
                    
                except Exception as el_err:
                    print(f"Error extracting data for product {i+1} on page {page_num}: {el_err}")
            
            # Check if there's a next page button and it's enabled
            next_button_exists = await page.is_visible(NEXT_PAGE_BUTTON_SELECTOR)
            if next_button_exists:
                next_button_disabled = await page.get_attribute(NEXT_PAGE_BUTTON_SELECTOR, "aria-disabled") == "true"
                if not next_button_disabled:
                    print("Moving to next page...")
                    
                    # Add random delay before clicking next page to appear more human-like
                    await page.wait_for_timeout(random.randint(2000, 4000))
                    await page.click(NEXT_PAGE_BUTTON_SELECTOR)
                    
                    # Wait for page to load content
                    await page.wait_for_load_state('domcontentloaded')
                    await page.wait_for_selector('li.WL_DZ', timeout=30000)
                    page_num += 1
                else:
                    print("Next button is disabled. No more pages.")
                    break
            else:
                print("No next page button found. End of products.")
                break
                
    except Exception as e:
        print(f"Error scraping products for {subcategory_name}: {e}")
        
    return products

async def scrape_subcategories(page, category_url, category_name):
    """
    Scrapes subcategories from a category page.
    
    Args:
        page: The Playwright page object
        category_url (str): The URL of the category page
        category_name (str): The name of the parent category
        
    Returns:
        tuple: (subcategories, products) lists containing subcategory and product data
    """
    subcategories = []
    all_products = []
    
    try:
        print(f"\nNavigating to category: {category_name} ({category_url})")
        await page.goto(category_url, wait_until='domcontentloaded', timeout=60000)
        print(f"Page loaded for {category_name}")
        
        # Wait for subcategory container
        print(f"Waiting for subcategory container: '{SUBCATEGORY_CONTAINER_SELECTOR}'")
        try:
            await page.wait_for_selector(SUBCATEGORY_CONTAINER_SELECTOR, timeout=30000)
            print("Subcategory container found.")
        except PlaywrightTimeoutError:
            print(f"No subcategory container found for '{category_name}'. Selector may be incorrect or page structure differs.")
            # If no subcategories, try to get products directly from category page
            print("Attempting to scrape products directly from category page...")
            category_products = await scrape_products(page, category_url, category_name, category_name)
            all_products.extend(category_products)
            return [], all_products
            
        # Extract subcategories
        subcategory_elements = page.locator(SUBCATEGORY_ITEM_SELECTOR)
        count = await subcategory_elements.count()
        print(f"Found {count} potential subcategories for {category_name}.")
        
        if count == 0:
            print(f"Warning: No subcategories found in {category_name}. Check the selector.")
            
        # Process each subcategory
        for i in range(count):
            element = subcategory_elements.nth(i)
            try:
                # Navigate back to category page for each subcategory
                await page.goto(category_url, wait_until='domcontentloaded', timeout=60000)
                
                # Re-locate the subcategory elements and get the current one
                subcategory_elements = page.locator(SUBCATEGORY_ITEM_SELECTOR)
                element = subcategory_elements.nth(i)
                
                subcategory_name = await element.get_attribute('aria-label') or ""
                if not subcategory_name:
                    label_selector = element.locator('.ddsweb-categoryPill__label')
                    subcategory_name = await label_selector.inner_text() if await label_selector.count() > 0 else "Name Not Found"
                
                subcategory_href = await element.get_attribute('href')
                subcategory_url = "URL Not Found"
                
                if subcategory_href:
                    if subcategory_href.startswith('/'):
                        base_url = "https://www.tesco.com"
                        subcategory_url = f"{base_url}{subcategory_href}"
                    else:
                        subcategory_url = subcategory_href

                # Skip offers and seasonal subcategories
                if category_url.lower() == subcategory_url.lower():
                    print(f"  Skipping all category link: {subcategory_name} ({subcategory_url})")
                    continue

                if "offers" in subcategory_url.lower():
                    print(f"  Skipping offers sub-category: {subcategory_name} ({subcategory_url})")
                    continue

                if "best-buys" in subcategory_url.lower():
                    print(f"  Skipping best-buys sub-category: {subcategory_name} ({subcategory_url})")
                    continue     

                if "spring-deals" in subcategory_url.lower():
                    print(f"  Skipping spring-deals sub-category: {subcategory_name} ({subcategory_url})")
                    continue                                

                if "easter" in subcategory_url.lower():
                    print(f"  Skipping easter sub-category: {subcategory_name} ({subcategory_url})")
                    continue                           
                        
                if subcategory_name != "Name Not Found" and subcategory_url != "URL Not Found":
                    subcategory_data = {
                        'parent_category': category_name,
                        'subcategory_name': subcategory_name,
                        'subcategory_url': subcategory_url
                    }
                    subcategories.append(subcategory_data)
                    print(f"  Extracted subcategory: {subcategory_name} ({subcategory_url})")
                    
                    # Scrape products for this subcategory
                    subcategory_products = await scrape_products(page, subcategory_url, subcategory_name, category_name)
                    all_products.extend(subcategory_products)
            except Exception as el_err:
                print(f"Error extracting subcategory data: {el_err}")
                
    except Exception as e:
        print(f"Error scraping subcategories for {category_name}: {e}")
        
    return subcategories, all_products

async def scrape_tesco_categories_playwright(url):
    """
    Scrapes main grocery categories from Tesco using Playwright.

    Args:
        url (str): The URL of the Tesco groceries page.

    Returns:
        tuple: (categories, subcategories, products) lists containing category, subcategory, and product data
    """
    categories = []
    all_subcategories = []
    all_products = []
    print("Launching Playwright browser...")
    async with async_playwright() as p:
        # Launch browser with basic configuration - keeping it simple
        try:
            # Use a more common viewport size
            browser = await p.chromium.launch(
                headless=False,  # Show the browser to see what's happening
                args=[
                    "--disable-blink-features=AutomationControlled",  # Hide automation
                    "--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36"
                ]
            )
            
            # Create a browser context with minimal parameters
            context = await browser.new_context(
                viewport={'width': 1366, 'height': 768},  # Common screen resolution
                user_agent='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36',
                locale='en-GB',  # Match Tesco's locale
                timezone_id='Europe/London',  # Set UK timezone
                
                # Simulate a real browser with permissions
                permissions=['geolocation', 'notifications'],
                
                # Emulate common device characteristics
                device_scale_factor=1.0,
                is_mobile=False,
                has_touch=False
            )
            
            # Minimal anti-detection script 
            await context.add_init_script("""
                () => {
                    // Overwrite the 'webdriver' property
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => false,
                        configurable: true
                    });
                    
                    // Overwrite plugins to seem like a regular browser
                    Object.defineProperty(navigator, 'plugins', {
                        get: () => [1, 2, 3, 4, 5],
                        configurable: true
                    });
                    
                    // Overwrite other automation flags
                    Object.defineProperty(navigator, 'languages', {
                        get: () => ['en-GB', 'en-US', 'en'],
                        configurable: true
                    });
                    
                    // Hide automation-related Chrome properties
                    window.chrome = {
                        runtime: {},
                    };
                }
            """)
            
            page = await context.new_page()

            async def route_handler(route):
                await route.continue_()
                if random.random() > 0.7:  # 30% chance of delay
                    await page.wait_for_timeout(random.randint(100, 600))
                    
            await page.route('**/*', route_handler)
            
            # Basic console logging
            page.on("pageerror", lambda err: print(f"Page error: {err}"))

            print(f"Navigating to {url}...")
            
            # Simple navigation approach that was working before
            try:
                # Use domcontentloaded which is more reliable than networkidle
                response = await page.goto(url, wait_until='domcontentloaded', timeout=60000)
                
                if response and response.ok:
                    print(f"Page loaded successfully with status: {response.status}")
                else:
                    print(f"Page returned non-OK status: {response.status if response else 'No response'}")
            except Exception as e:
                print(f"Navigation error: {e}")
                # Take screenshot to see what's happening
                await page.screenshot(path="tesco_navigation_error.png")
                print("Screenshot saved as tesco_navigation_error.png")
            
            # Handle cookie consent prompt
            try:
                print("Looking for cookie consent prompt...")
                cookie_button_selector = COOKIE_CONSENT_SELECTOR
                
                # Wait for cookie consent button with a reasonable timeout
                is_cookie_visible = await page.is_visible(cookie_button_selector, timeout=5000)
                if is_cookie_visible:
                    print("Cookie consent prompt found, clicking 'Accept all'...")
                    await page.click(cookie_button_selector)
                    print("Cookie consent accepted.")
                    await page.wait_for_timeout(3000)
                else:
                    print("No cookie consent prompt detected.")
            except Exception as cookie_err:
                print(f"Error handling cookie prompt: {cookie_err}. Continuing with scraping...")

            # Wait for category container
            print(f"Waiting for category container selector: '{CATEGORY_CONTAINER_SELECTOR}'")
            try:
                await page.wait_for_selector(CATEGORY_CONTAINER_SELECTOR, timeout=30000)
                print("Category container found.")
            except PlaywrightTimeoutError:
                print(f"Timeout Error: Could not find selector '{CATEGORY_CONTAINER_SELECTOR}' after 30 seconds.")
                # Take a screenshot for debugging
                await page.screenshot(path="tesco_timeout_error.png")
                print("Screenshot saved as tesco_timeout_error.png")
                await browser.close()
                return [], [], []

            # Extract category data
            print(f"Locating category items using selector: '{CATEGORY_ITEM_SELECTOR}'")
            category_elements = page.locator(CATEGORY_ITEM_SELECTOR)
            count = await category_elements.count()
            print(f"Found {count} potential category items.")

            if count == 0:
                 print(f"Warning: Found 0 items matching '{CATEGORY_ITEM_SELECTOR}'. Check the selector.")
                 # Take a screenshot to see what's on the page
                 await page.screenshot(path="tesco_no_categories.png")
                 print("Screenshot saved as tesco_no_categories.png")

            # Iterate through the located elements
            for i in range(count):
                # Navigate back to main page for each category
                await page.goto(url, wait_until='domcontentloaded', timeout=60000)
                
                # Re-locate the category elements after navigation
                category_elements = page.locator(CATEGORY_ITEM_SELECTOR)
                element = category_elements.nth(i)
                
                try:
                    # Get category name from the span text or aria-label
                    span_element = element.locator('span.ddsweb-link__text')
                    category_name = await span_element.inner_text() if await span_element.count() > 0 else ""
                    
                    if not category_name:
                        category_name = await element.get_attribute('aria-label') or "Name Not Found"
                    
                    category_href = await element.get_attribute('href')
                    category_url = "URL Not Found"

                    if category_href:
                        if category_href.startswith('/'):
                            base_url = "https://www.tesco.com"
                            category_url = f"{base_url}{category_href}"
                        else:
                            category_url = category_href

                    # Skip categories with "offers" or seasonal categories in the URL
                    if "food-cupboard" not in category_url.lower():
                        print(f"  Skipping non specific category: {category_name} ({category_url})")
                        continue

                    if "marketplace" in category_url.lower():
                        print(f"  Skipping offers marketplace: {category_name} ({category_url})")
                        continue

                    if "offers" in category_url.lower():
                        print(f"  Skipping offers category: {category_name} ({category_url})")
                        continue

                    if "easter" in category_url.lower():
                        print(f"  Skipping easter category: {category_name} ({category_url})")
                        continue                   

                    if "best-buys" in category_url.lower():
                        print(f"  Skipping best-buys category: {category_name} ({category_url})")
                        continue

                    if "spring-deals" in category_url.lower():
                        print(f"  Skipping spring-deals category: {category_name} ({category_url})")
                        continue

                    if category_name != "Name Not Found" and category_url != "URL Not Found":
                        category_data = {
                            'category_name': category_name,
                            'category_url': category_url
                        }
                        categories.append(category_data)
                        print(f"  Extracted category: {category_name} ({category_url})")
                        
                        # Scrape subcategories and products for this category
                        subcategories, products = await scrape_subcategories(page, category_url, category_name)
                        all_subcategories.extend(subcategories)
                        all_products.extend(products)
                    else:
                        print("  Failed to extract name for an element.")

                except Exception as el_err:
                    print(f"Error extracting data from one element: {el_err}")

        except PlaywrightTimeoutError:
            print(f"Navigation Timeout Error: Page {url} took too long to load.")
            # Take a screenshot for debugging
            if 'page' in locals():
                await page.screenshot(path="tesco_timeout_error.png")
                print("Screenshot saved as tesco_timeout_error.png")
        except Exception as e:
            print(f"An error occurred during Playwright scraping: {e}")
            # Take a screenshot for debugging
            if 'page' in locals():
                await page.screenshot(path="tesco_error.png")
                print("Screenshot saved as tesco_error.png")
        finally:
            if 'browser' in locals() and browser.is_connected():
                await browser.close()
                print("Browser closed.")

    return categories, all_subcategories, all_products

def save_to_csv(data, filename, description="data"):
    """Saves the scraped data to a CSV file."""
    if not data:
        print(f"No {description} to save.")
        return

    # Define the field names based on the keys in the dictionaries
    if data:
        fieldnames = data[0].keys()
    else:
        print(f"Cannot determine field names, no {description}.")
        return

    try:
        with open(filename, 'w', newline='', encoding='utf-8') as csvfile:
            writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
            writer.writeheader()
            writer.writerows(data)
        print(f"Successfully saved {description} to {filename}")
    except IOError as e:
        print(f"Error writing to CSV file {filename}: {e}")
    except Exception as e:
        print(f"An unexpected error occurred during CSV writing: {e}")


# --- Main Execution ---
if __name__ == "__main__":
    print("Starting Tesco category, subcategory, and product scraper using Playwright...")
    # Run the asynchronous function
    start_time = time.time()
    categories, subcategories, products = asyncio.run(scrape_tesco_categories_playwright(URL))
    end_time = time.time()

    if categories:
        print(f"\nScraped {len(categories)} main categories in {end_time - start_time:.2f} seconds.")
        save_to_csv(categories, "tesco_categories_playwright.csv", "categories")
        
    if subcategories:
        print(f"Scraped {len(subcategories)} subcategories.")
        save_to_csv(subcategories, "tesco_subcategories_playwright.csv", "subcategories")
        
    if products:
        print(f"Scraped {len(products)} products.")
        save_to_csv(products, "tesco_products_playwright.csv", "products")
        
    if not categories and not subcategories and not products:
        print("\nPlaywright scraping finished, but no data was extracted.")
        print("Please double-check the selectors by inspecting the website in your browser.")

    print("\nScript finished.")