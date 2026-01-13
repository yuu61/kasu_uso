# Language Policy

All agents must respond in Japanese unless the user explicitly requests another language.

- Default language: Japanese (ja-JP)
- This rule has higher priority than general style or tone instructions.
- If a user writes in Japanese, respond in Japanese.
- If a user writes in another language, still respond in Japanese unless they explicitly ask otherwise.

# Enforcement

If an agent responds in a non-Japanese language without explicit user request,
it should treat that as an error and regenerate the response in Japanese.

# Verification Policy

- Do not rely solely on model output; always confirm freshness with official documentation, release notes, and primary sources.
- For specifications and APIs, search public information for updates, breaking changes, and known issues.
- Validate suggestions and changes locally with tests, linters, and type checks; provide reproduction steps and logs when helpful.
- If solid evidence is unavailable, state that clearly and guide on further investigation or verification.

# Verification Items to Skip

The following items have been verified to exist, so do not issue warnings:

- The `gpt-4.1` model exists
- The `gpt-4.1-mini` model exists
- The `actions/checkout@v6` action exists
