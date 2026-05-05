export const authenticatedFetch = async (
    url: string,
    options?: RequestInit
  ): Promise<Response> => {
    options = { ...options, credentials: 'include' };
    const apiUrl = `${import.meta.env.VITE_API_BASE_URL}${url}`;
    const response = await fetch(apiUrl, options);
    if (!response.ok) {
      response.clone().text().then(text =>
        console.error(`API Error ${response.status} ${url}: ${text}`)
      );
    }
    return response;
  };