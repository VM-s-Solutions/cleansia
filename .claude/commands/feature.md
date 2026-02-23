# Feature Implementation Command

Implement a new feature across the full stack (backend, frontend, mobile).

## Usage

```
/feature [feature_description]
```

## Instructions

You are now acting as the Orchestrator Agent coordinating a full-stack feature implementation.

## Workflow

### Phase 1: Planning
1. Analyze the feature requirements
2. Identify affected platforms (backend/frontend/mobile)
3. Break down into tasks per platform
4. Identify dependencies between tasks

### Phase 2: Backend Implementation
1. Create database entities and migrations
2. Create DTOs (as records)
3. Create Commands/Queries with nested structure
4. Add API endpoints
5. Follow CODING_STANDARDS.md strictly

### Phase 3: Code Sync
1. Generate TypeScript interfaces from C# DTOs
2. Generate Kotlin data classes from C# DTOs
3. Update API service clients

### Phase 4: Frontend Implementation
1. Create NgRx state (if needed)
2. Create Facade for state management
3. Create components with OnPush
4. Add translations for all text
5. Never use enum values directly in templates

### Phase 5: Mobile Implementation
1. Create ViewModel with HiltViewModel
2. Create Screen composables
3. Add navigation routes
4. Use string resources for all text

### Phase 6: Code Review
1. Review all changes against CODING_STANDARDS.md
2. Check for:
   - Nested class structure in Commands/Queries
   - No CommitAsync in handlers
   - DTOs as records
   - Translations used in frontend
   - String resources in mobile
3. Report any violations

### Phase 7: Documentation
1. Update CHANGELOG.md
2. Update API documentation
3. Update README if needed

## Output

Provide a comprehensive implementation with:
- Clear separation between platforms
- All code following coding standards
- Summary of all files created/modified

## Example

```
/feature Add employee time tracking with start/stop functionality
```
