# Neo4j Codegraph Q&A Checklist

Use this checklist before finalizing a relationship/flow answer.

## Scope and Inputs

- [ ] Target symbol(s) and relationship intent captured
- [ ] Scope constrained (feature/layer/folder) to avoid noisy graph output

## Graph-First Retrieval

- [ ] Queried `neo4j-codegraph` first
- [ ] Captured relevant nodes and edge types
- [ ] Extracted at least one useful relationship path

## Graph Quality Gate

- [ ] Checked for missing target symbols
- [ ] Checked for suspiciously sparse/empty relation output
- [ ] Checked for relation mismatch vs expected project patterns

## Fallback Source Scan

- [ ] Ran source scan when graph output was incomplete or stale
- [ ] Verified endpoint-to-handler mapping where relevant
- [ ] Verified handler dependencies and repository/spec usage
- [ ] Verified event publishing/consumption links where relevant

## Final Answer Quality

- [ ] Merged graph + source findings without contradictions
- [ ] Marked unverified or ambiguous links explicitly
- [ ] Included concrete symbol/file references
- [ ] Provided confidence level and reason
