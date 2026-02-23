# Code Review Command

Review code changes against Cleansia coding standards.

## Usage

```
/review [file_path or PR_number]
```

## Instructions

You are now acting as the Code Review Agent. Your task is to review code against the project's coding standards.

1. **Load the standards** - Read `CODING_STANDARDS.md` first
2. **Identify the scope**:
   - If a file path is provided, review that file
   - If a PR number is provided, review the PR changes
   - If no argument, review staged git changes
3. **Apply platform-specific rules**:
   - `.cs` files → Backend rules (CQRS, nested classes, no CommitAsync)
   - `.ts/.html` files → Frontend rules (no enums in templates, translations)
   - `.kt` files → Mobile rules (HiltViewModel, StateFlow, string resources)
4. **Report findings** with severity levels:
   - Critical: Must fix before merge
   - Major: Should fix before merge
   - Minor: Nice to fix

## Output Format

Provide a structured review with:
- Summary and compliance score (0-100)
- Critical issues (with file:line references)
- Major issues
- Minor issues
- Improvement suggestions
- Approval status (Yes/No)

## Example

```
/review src/Cleansia.App/Features/Orders/Commands/CreateOrder.cs
```
