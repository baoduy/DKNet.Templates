# Release notes

**Breaking change — status-counts default window (DRK-521).** Status-counts endpoints (the `GET`
route `MapGetStatusCounts<TEntity>` maps, at the `status` segment by default) no longer default to
the last 30 days when no `from`/`to` bounds are supplied. An unbounded call now reports counts over
the **entire history**. Explicit `from`/`to` bounds are unaffected — pass them to preserve a bounded
window.
