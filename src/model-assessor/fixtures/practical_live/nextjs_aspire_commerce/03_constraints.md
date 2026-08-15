# Commerce Constraints And Data Notes

- Redis is a cache for product catalogue and supporting datasets, not the primary system of record.
- Supporting datasets can include curated content, category trees, shipping regions, promotions, and tax or pricing lookup inputs.
- The response should explicitly distinguish the AppHost from the application services it orchestrates.
- The response should explain what belongs in the Next.js application layer versus background workers or data-refresh jobs.
