---
name: qdrant-docsearch-checklist
description: Quality checklist for qdrant-docsearch skill answers
---

# Qdrant Doc Search — Quality Checklist

## Search Quality

- [ ] Query was derived from the user's actual question, not a generic keyword
- [ ] Multiple search queries were tried if the first returned low-relevance results
- [ ] Results from different categories (specs, skills, docs) were considered

## Source Verification

- [ ] At least one source file was read directly to verify vector chunk accuracy
- [ ] File paths in the answer are valid and point to existing files
- [ ] Quoted content matches what is actually in the source files
- [ ] Cross-references between documents were followed where relevant

## Answer Quality

- [ ] Answer directly addresses the user's question
- [ ] Confidence level is stated (high/medium/low)
- [ ] Gaps or unknowns are clearly marked
- [ ] File paths are provided for further reading
- [ ] No speculative information presented as documented fact

## Fallback Handling

- [ ] Fallback to file reads was used when vector results were insufficient
- [ ] Both vector and file-read findings were reconciled in the final answer
- [ ] Missing documentation was flagged if the topic has no coverage
