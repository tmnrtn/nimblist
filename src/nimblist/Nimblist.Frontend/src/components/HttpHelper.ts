export const authenticatedFetch = async (
    url: string,
    options?: RequestInit
  ): Promise<Response> => {
    options = { ...options, credentials: 'include' }; // Add if needed for cookie auth + CORS
    const apiUrl = `${import.meta.env.VITE_API_BASE_URL}${url}`;
    console.log(`Making request to: ${apiUrl}`);
    const response = await fetch(apiUrl, options);
    if (!response.ok) {
      const errorText = await response.text();
      console.error(`API Error ${response.status}: ${errorText}`);
      throw new Error(`Request failed: ${response.status}`);
    }
    // Only return body for requests that have one (e.g. GET, POST)
    // For 204 No Content (like our PUT toggle), there's no body
    // if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    //    return null as T; // Or just return the response status/object
    // }
    // return response.json() as Promise<T>; // Example if expecting JSON
    return response; // Keep it simple for now
  };