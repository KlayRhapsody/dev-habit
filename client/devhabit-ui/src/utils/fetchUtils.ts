type RequestInit = Parameters<typeof fetch>[1];

interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
}

class ApiError extends Error {
  details?: ProblemDetails;

  constructor(message: string, details?: ProblemDetails) {
    super(message);
    this.details = details;
  }
}

export async function fetchWithAuth<T>(
  url: string,
  accessToken: string | null,
  options: RequestInit = {}
): Promise<T> {
  if (!accessToken) {
    throw new Error('No access token available');
  }

  // Only set Content-Type if we're not sending FormData
  const defaultHeaders: Record<string, string> = {
    Authorization: `Bearer ${accessToken}`,
  };

  if (!(options.body instanceof FormData)) {
    defaultHeaders['Content-Type'] = 'application/json';
  }

  const response = await fetch(url, {
    ...options,
    headers: {
      ...defaultHeaders,
      ...options.headers,
    },
  });

  if (!response.ok) {
    // Try to get error message from response
    const errorData = await response.json();
    throw new ApiError(
      errorData.detail || errorData.message || `HTTP error! status: ${response.status}`,
      errorData
    );
  }

  // Return null for 204 responses or empty bodies, otherwise parse JSON
  if (response.status === 204) {
    return null as T;
  }
  try {
    return await response.json();
  } catch {
    return null as T;
  }
}
