# Skill Template: [SKILL TITLE]

**Copy this file to `<skill-folder>/skill.md` and fill in all sections below.**

---

## Overview

**When to use this skill**: [1-2 sentences describing when a developer should follow this skill]

**What you'll create**: [1-2 sentences describing the deliverables]

**Estimated time**: [min–max minutes from metadata.json]

This skill is part of the DKNet.Templates feature delivery workflow. See [CONVENTIONS.md](../CONVENTIONS.md) for folder structure and publishing rules.

---

## Prerequisites: Do You Know This?

Before starting, ensure you have:

- [ ] Read [AGENTS.md - Architecture Overview](../../../AGENTS.md)
- [ ] Familiarity with [C# classes and properties] ← **REPLACE WITH YOUR PREREQUISITES**
- [ ] Understanding of [relevant technology/pattern] ← **REPLACE**
- [ ] Access to [tools/environment needed] ← **REPLACE**

If you don't have these, take 10 minutes to review them first—this skill builds on these foundations.

---

## Inputs Checklist: Gather This Information First

Before you start following the step-by-step workflow, collect this information:

- [ ] **[Input 1 Name]**: [Description of what this is and where to find it]
  - Example: Entity name (PascalCase): `PurchaseOrder`
  
- [ ] **[Input 2 Name]**: [Description]
  - Example: List of properties with types

- [ ] **[Input 3 Name]**: [Description]

*Don't make up this information as you go—gather it beforehand. It will make the workflow 10x smoother.*

---

## Step-by-Step Workflow

### Step 1: [Action Title]

**What you're doing**: [Why this step matters in 1 sentence]

1. Perform this action: [Specific, numbered sub-steps]
2. Verify result: [How to check you did it correctly]

**Code Example** (copy, then customize with YOUR values):
```csharp
// THIS IS A TEMPLATE - adapt to your situation
public sealed class YourEntityName
{
    // Your code here
}
```

**Common mistake**: [What developers often get wrong in this step]  
**Fix**: [How to correct it]

---

### Step 2: [Action Title]

**What you're doing**: [Why this step matters]

[Repeat structure from Step 1]

---

### Step 3: [Action Title]

[Continue with additional steps...]

---

### Step N: Validate Your Work

Before you consider this skill complete, run the **validation checklist** below:

```bash
cd /Users/steven/_CODE/GIT/DKNet.Templates
# Copy checklist items and verify each one
```

---

## Success Validation: Checklist

Print or copy the checklist from [checklist.md](./checklist.md) and verify ALL items are complete:

- [ ] Item 1
- [ ] Item 2
- [ ] Item 3

**If any item fails**: Refer to the remediation guidance in [checklist.md](./checklist.md).

---

## Common Errors & How to Fix Them

### Error: "[ERROR MESSAGE]"

**Why it happens**: [Explanation of root cause]

**How to fix**:
1. [Step 1 of fix]
2. [Step 2 of fix]
3. [Verify the fix]

**Prevention**: [How to avoid this in the future]

---

### Error: "[ANOTHER ERROR]"

[Repeat structure above for 3-5 common errors]

---

## Complete Working Example

See the files in `./examples/` for a complete, production-ready example:

- **[example-file-1.cs](./examples/[example-folder]/ExampleFile1.cs)**: [What this file does]
- **[example-file-2.cs](./examples/[example-folder]/ExampleFile2.cs)**: [What this file does]
- **[README.md](./examples/[example-folder]/README.md)**: Line-by-line explanation of the example

**Copy-paste strategy**: Use these examples as templates; customize entity/class names for your use case.

---

## Testing Your Work (Optional)

If you've written the code correctly, existing tests should pass:

```bash
cd /Users/steven/_CODE/GIT/DKNet.Templates/src
dotnet test Minimal.App.Tests --filter "SkillName"
```

Expected output: All tests pass ✅

---

## Next Steps: Continue the Feature Workflow

Once you've completed this skill, you're ready for:

1. **[Next Skill Name]**: [Link to next skill](../[next-skill-folder]/skill.md)
   - What it does: [1 sentence]
   - Why it's next: [1 sentence]

2. **[Alternative Skill Name]**: [Link if branching paths exist]

---

## Questions or Issues?

- 📖 Review [CONVENTIONS.md](../CONVENTIONS.md) for project-wide rules
- 🔍 Search [CATALOG.md](../CATALOG.md) for related topics
- 🐛 Found a bug? Create an issue titled: `[SKILL] [skill-name]: [issue description]`
- 👥 Questions? Comment in PR or contact maintainers

---

**Skill Version**: 1.0.0  
**Last Updated**: [DATE]  
**Status**: Published
