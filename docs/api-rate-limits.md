# API rate limits

Plattarr limits each client's `/api` requests to a burst of 120, replenished at
10 requests per second. Form sign-in accepts 10 POST requests to `/login` per
minute per client. Other UI pages and SignalR are outside these limits.

The client is identified by the connection IP address after trusted proxy
headers are processed. Multiple users behind one address share a limit. A
rejected request returns HTTP 429 and a `Retry-After` header; API responses
include a short JSON error message. Clients should wait for the indicated
interval before retrying.

These in-memory limits apply separately to each Plattarr process. They reduce
accidental or abusive request bursts; authentication and network access
controls remain necessary.
