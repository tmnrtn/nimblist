import asyncio
from playwright.async_api import async_playwright, TimeoutError as PlaywrightTimeoutError
import csv
import time
import re

# Target URL for Waitrose groceries
URL = "https://www.waitrose.com/ecom/shop/browse/groceries"

# --- IMPORTANT: Selectors ---
# Main category selectors
CATEGORY_CONTAINER_SELECTOR = 'ul[data-testid="category-list-links"]'
CATEGORY_ITEM_SELECTOR = 'li a'

# Subcategory selectors
SUBCATEGORY_CONTAINER_SELECTOR = 'ul[data-testid="category-list-links"]'
SUBCATEGORY_ITEM_SELECTOR = 'li a'

# Product selectors
PRODUCT_LIST_SELECTOR = 'div[data-testid="product-list"]'
PRODUCT_ITEM_SELECTOR = 'article[data-testid="product-pod"]'
LOAD_MORE_BUTTON_SELECTOR = 'button[aria-label="Load more"]'

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
        print(f"\nNavigating to subcategory: {subcategory_name} ({subcategory_url})")
        await page.goto(subcategory_url, wait_until='domcontentloaded', timeout=60000)
        print(f"Page loaded for {subcategory_name}")
        
        # Wait for product list container
        print(f"Waiting for product list container: '{PRODUCT_LIST_SELECTOR}'")
        try:
            await page.wait_for_selector(PRODUCT_LIST_SELECTOR, timeout=30000)
            print("Product list container found.")
        except PlaywrightTimeoutError:
            print(f"No product list found for '{subcategory_name}'. Selector may be incorrect or page structure differs.")
            return []
        
        # Click "Load more" button until all products are loaded
        load_more_attempts = 0
        max_load_more_attempts = 20  # Adjust based on expected maximum number of pages
        
        while load_more_attempts < max_load_more_attempts:
            try:
                # Check if "Load more" button exists
                load_more_visible = await page.is_visible(LOAD_MORE_BUTTON_SELECTOR)
                if not load_more_visible:
                    print("No more products to load.")
                    break
                
                print("Clicking 'Load more' button...")
                await page.click(LOAD_MORE_BUTTON_SELECTOR)
                await page.wait_for_timeout(2000)  # Wait for new products to load
                load_more_attempts += 1
            except Exception as e:
                print(f"Finished loading products: {e}")
                break
        
        # Now extract all products
        print("Extracting product information...")
        product_elements = page.locator(PRODUCT_ITEM_SELECTOR)
        count = await product_elements.count()
        print(f"Found {count} products in {subcategory_name}.")
        
        if count == 0:
            print(f"Warning: No products found in {subcategory_name}. Check the selector.")
        
        # Process each product
        for i in range(count):
            element = product_elements.nth(i)
            try:
                # Extract product details using data attributes
                product_id = await element.get_attribute("data-product-id")
                product_name = await element.get_attribute("data-product-name")
                product_availability = await element.get_attribute("data-product-availability")
                product_on_offer = await element.get_attribute("data-product-on-offer")
                
                # Extract price
                price_selector = element.locator('span[data-test="product-pod-price"]')
                price_text = await price_selector.inner_text() if await price_selector.count() > 0 else "Price not available"
                # Clean up price text (remove "Item price" and extract the numerical value)
                price = re.sub(r'Item price|[^0-9£.]', '', price_text).strip()
                
                # Extract price per unit
                price_per_unit_selector = element.locator('.pricePerUnit___a1PxI')
                price_per_unit = await price_per_unit_selector.inner_text() if await price_per_unit_selector.count() > 0 else ""
                
                # Extract product size
                size_selector = element.locator('span[data-testid="product-size"]')
                size = await size_selector.inner_text() if await size_selector.count() > 0 else ""
                
                # Extract product URL
                url_selector = element.locator('a[data-origincomponent="ProductPod"]')
                product_href = await url_selector.get_attribute('href') if await url_selector.count() > 0 else None
                product_url = ""
                if product_href:
                    if product_href.startswith('/'):
                        base_url = "/".join(subcategory_url.split("/")[:3])
                        product_url = f"{base_url}{product_href}"
                    else:
                        product_url = product_href
                
                # Extract product image URL
                img_selector = element.locator('img')
                img_url = await img_selector.get_attribute('src') if await img_selector.count() > 0 else ""
                
                # Create product data object
                product_data = {
                    'parent_category': parent_category,
                    'subcategory': subcategory_name,
                    'product_id': product_id,
                    'product_name': product_name,
                    'price': price,
                    'price_per_unit': price_per_unit,
                    'size': size,
                    'availability': product_availability,
                    'on_offer': product_on_offer,
                    'product_url': product_url,
                    'image_url': img_url
                }
                products.append(product_data)
                print(f"  Extracted product: {product_name} ({price})")
            except Exception as el_err:
                print(f"Error extracting product data: {el_err}")
                
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
            return [], []
            
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
                await page.goto(category_url, wait_until='domcontentloaded', timeout=60000)
                subcategory_name = await element.inner_text()
                subcategory_href = await element.get_attribute('href')
                
                subcategory_name = subcategory_name.strip() if subcategory_name else "Name Not Found"
                subcategory_url = "URL Not Found"
                
                if subcategory_href:
                    if subcategory_href.startswith('/'):
                        base_url = "/".join(category_url.split("/")[:3])
                        subcategory_url = f"{base_url}{subcategory_href}"
                    else:
                        subcategory_url = subcategory_href

                if "offers" in subcategory_url.lower():
                    print(f"  Skipping offers sub-category: {subcategory_name} ({subcategory_url})")
                    continue

                if "easter" in subcategory_url.lower():
                    print(f"  Skipping easter sub-category: {subcategory_name} ({subcategory_url})")
                    continue                           
                        
                if subcategory_name != "Name Not Found":
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

async def scrape_waitrose_categories_playwright(url):
    """
    Scrapes main grocery categories from Waitrose using Playwright.

    Args:
        url (str): The URL of the Waitrose groceries page.

    Returns:
        tuple: (categories, subcategories, products) lists containing category, subcategory, and product data
    """
    categories = []
    all_subcategories = []
    all_products = []
    print("Launching Playwright browser...")
    async with async_playwright() as p:
        # Launch browser (Chromium is default). headless=False shows the browser window.
        try:
            browser = await p.chromium.launch(headless=False)
            context = await browser.new_context(
                user_agent='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
            )
            page = await context.new_page()

            print(f"Navigating to {url}...")
            await page.goto(url, wait_until='domcontentloaded', timeout=60000)
            print("Page loaded initially.")

            # Handle cookie consent prompt
            try:
                print("Looking for cookie consent prompt...")
                cookie_button_selector = 'button[data-testid="accept-all"]'
                
                # Wait for cookie consent button with a reasonable timeout
                is_cookie_visible = await page.is_visible(cookie_button_selector, timeout=5000)
                if is_cookie_visible:
                    print("Cookie consent prompt found, clicking 'Allow all'...")
                    await page.click(cookie_button_selector)
                    print("Cookie consent accepted.")
                    # Give the page some time to process after accepting cookies
                    await page.wait_for_timeout(2000)
                else:
                    print("No cookie consent prompt detected.")
            except Exception as cookie_err:
                print(f"Error handling cookie prompt: {cookie_err}. Continuing with scraping...")


            # --- Wait for dynamic content ---
            print(f"Waiting for category container selector: '{CATEGORY_CONTAINER_SELECTOR}'")
            try:
                await page.wait_for_selector(CATEGORY_CONTAINER_SELECTOR, timeout=30000)
                print("Category container found.")
            except PlaywrightTimeoutError:
                print(f"Timeout Error: Could not find selector '{CATEGORY_CONTAINER_SELECTOR}' after 30 seconds.")
                print("The page might not have loaded correctly, or the selector is wrong.")
                await browser.close()
                return [], [], []

            # --- Extract category data ---
            print(f"Locating category items using selector: '{CATEGORY_ITEM_SELECTOR}'")
            category_elements = page.locator(CATEGORY_ITEM_SELECTOR)
            count = await category_elements.count()
            print(f"Found {count} potential category items.")

            if count == 0:
                 print(f"Warning: Found 0 items matching '{CATEGORY_ITEM_SELECTOR}'. Check the selector.")

            # Iterate through the located elements
            for i in range(count):
                element = category_elements.nth(i)
                try:
                    await page.goto(url, wait_until='domcontentloaded', timeout=60000)
                    category_name = await element.inner_text()
                    category_href = await element.get_attribute('href')

                    category_name = category_name.strip() if category_name else "Name Not Found"
                    category_url = "URL Not Found"

                    if category_href:
                        if category_href.startswith('/'):
                            base_url = "/".join(url.split("/")[:3])
                            category_url = f"{base_url}{category_href}"
                        else:
                            category_url = category_href

                    # Skip categories with "offers" in the URL
                    if "offers" in category_url.lower():
                        print(f"  Skipping offers category: {category_name} ({category_url})")
                        continue

                    if "easter" in category_url.lower():
                        print(f"  Skipping easter category: {category_name} ({category_url})")
                        continue                   

                    if category_name != "Name Not Found":
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
        except Exception as e:
            print(f"An error occurred during Playwright scraping: {e}")
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
    print("Starting Waitrose category, subcategory, and product scraper using Playwright...")
    # Run the asynchronous function
    start_time = time.time()
    categories, subcategories, products = asyncio.run(scrape_waitrose_categories_playwright(URL))
    end_time = time.time()

    if categories:
        print(f"\nScraped {len(categories)} main categories in {end_time - start_time:.2f} seconds.")
        save_to_csv(categories, "waitrose_categories_playwright.csv", "categories")
        
    if subcategories:
        print(f"Scraped {len(subcategories)} subcategories.")
        save_to_csv(subcategories, "waitrose_subcategories_playwright.csv", "subcategories")
        
    if products:
        print(f"Scraped {len(products)} products.")
        save_to_csv(products, "waitrose_products_playwright.csv", "products")
        
    if not categories and not subcategories and not products:
        print("\nPlaywright scraping finished, but no data was extracted.")
        print("Please double-check the selectors by inspecting the website in your browser.")

    print("\nScript finished.")

