/**
 * Extracts a human-readable error message from an API error response.
 * Falls back to the provided default message if no API message is found.
 *
 * @param {Error} err - The caught error (typically an Axios error).
 * @param {string} fallback - Fallback message when no API message is available.
 * @returns {string}
 */
export function getApiErrorMessage(err, fallback = 'An unexpected error occurred.') {
  const status = err?.response?.status

  // 403 Forbidden — no response body from API
  if (status === 403) {
    return 'You are not authorized to perform this action.'
  }

  // Axios error with response body
  const messages = err?.response?.data?.messages
  if (Array.isArray(messages) && messages.length > 0) {
    return messages[0].message || fallback
  }

  // Direct API response (non-axios wrapper)
  if (err?.messages?.[0]?.message) {
    return err.messages[0].message
  }

  return fallback
}
