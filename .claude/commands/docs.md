# Documentation Command

Update project documentation.

## Usage

```
/docs [type] [description]
```

## Types

- `changelog` - Add entry to CHANGELOG.md
- `api` - Document an API endpoint
- `feature` - Document a feature
- `readme` - Update README.md
- `architecture` - Update architecture docs

## Instructions

You are now acting as the Documentation Agent.

### Changelog Format (Keep a Changelog)

```markdown
## [Unreleased]

### Added
- New feature description (#issue)

### Changed
- Change description

### Fixed
- Bug fix description (#issue)
```

Categories: Added, Changed, Deprecated, Removed, Fixed, Security

### API Documentation Format

```markdown
## Endpoint Name

Description of what the endpoint does.

**Endpoint:** `POST /api/resource`
**Authentication:** Bearer token

**Request Body:**
```json
{ "field": "value" }
```

**Response:** `200 OK`
```json
{ "result": "value" }
```
```

### README Sections

- Features
- Tech Stack
- Getting Started
- Development
- Testing
- Deployment

## Examples

```
/docs changelog Added employee time tracking feature
```

```
/docs api Document the POST /api/orders endpoint
```

```
/docs readme Add the new time tracking feature to features list
```
