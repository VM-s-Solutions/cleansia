# Cleansia Styles Organization

This directory contains all SCSS styles for the Cleansia application, organized by type.

## Directory Structure

```
styles/
├── common/                  # Common/shared styles
│   ├── error.scss
│   ├── font.scss
│   ├── index.scss
│   ├── sizing.scss
│   └── variables.scss
├── components/              # Shared component styles
│   ├── index.scss                           # Imports all component styles
│   ├── cleansia-availability.component.scss
│   ├── cleansia-brand-name.component.scss
│   ├── cleansia-button.component.scss
│   ├── cleansia-dynamic-background.component.scss
│   ├── cleansia-file.component.scss
│   ├── cleansia-language-switcher.component.scss
│   ├── cleansia-loader.component.scss
│   ├── cleansia-registration-lock.component.scss
│   ├── cleansia-section.component.scss
│   ├── cleansia-select.component.scss
│   ├── cleansia-sidebar-menu.component.scss
│   ├── cleansia-table.component.scss
│   ├── cleansia-telephone.component.scss
│   └── cleansia-title.component.scss
├── pages/                   # Page-specific styles
│   └── cleansia-partner/    # Partner portal pages
│       ├── index.scss                       # Imports all partner page styles
│       ├── confirm-email.component.scss
│       ├── invoice-detail.component.scss
│       ├── invoices.component.scss
│       ├── login.component.scss
│       ├── order-details.component.scss
│       ├── orders.component.scss
│       ├── profile.component.scss
│       └── register.component.scss
├── cleansia-partner.scss    # Partner portal entry point (imports all)
└── index.scss               # Global styles entry point
```

## Organization Rules

### Components (`components/`)
- Contains styles for reusable shared components
- Located in `libs/shared/components/src/lib/*`
- All component styles are imported globally through `components/index.scss`
- Components **do not** use `styleUrls` - all styles are applied globally

### Pages (`pages/cleansia-partner/`)
- Contains styles for feature-specific pages
- Located in `libs/cleansia-partner-features/*/src/lib/*`
- All page styles are imported globally through `pages/cleansia-partner/index.scss`
- Pages **do not** use `styleUrls` - all styles are applied globally

### Common (`common/`)
- Shared utilities, variables, mixins, and base styles
- Imported globally through main stylesheets

## Global Import Strategy

All styles are imported globally through [cleansia-partner.scss](cleansia-partner.scss):

```scss
// 1. Common styles (variables, fonts, etc.)
@use './common';

// 2. Shared component styles
@import './components/index.scss';

// 3. Partner portal page styles
@import './pages/cleansia-partner/index.scss';
```

This approach ensures:
- **No component-level styleUrls**: Cleaner component decorators
- **Global availability**: All styles available throughout the application
- **Single source of truth**: One place to manage all style imports
- **Better build optimization**: Angular can optimize a single stylesheet better than multiple component-level styles

## Benefits of This Structure

1. **Centralized Management**: All styles in one location for easier maintenance
2. **Clear Separation**: Components vs Pages clearly distinguished
3. **Scalability**: Easy to add new page types (e.g., `pages/admin/`, `pages/customer/`)
4. **Consistency**: Enforces consistent styling patterns across the application
5. **Performance**: Global styles are loaded once and cached
6. **Build Optimization**: Single stylesheet is easier for build tools to optimize
7. **Cleaner Components**: No styleUrls clutter in component decorators

## Adding New Styles

### New Component
1. Create the SCSS file in `components/` directory
2. Add import to `components/index.scss`:
   ```scss
   @import './your-component.component.scss';
   ```
3. Component decorator **does not** need `styleUrls`:
   ```typescript
   @Component({
     selector: 'your-component',
     templateUrl: './your-component.component.html',
     standalone: true,
   })
   ```

### New Page
1. Create the SCSS file in appropriate `pages/` subfolder
2. Add import to that subfolder's `index.scss`:
   ```scss
   @import './your-page.component.scss';
   ```
3. Component decorator **does not** need `styleUrls`:
   ```typescript
   @Component({
     selector: 'your-page',
     templateUrl: './your-page.component.html',
     standalone: true,
   })
   ```

### New Page Type
1. Create new folder under `pages/` (e.g., `pages/admin/`)
2. Create `index.scss` in that folder to import all page styles
3. Add import to main stylesheet (e.g., create `cleansia-admin.scss`)

## Migration Notes

All component SCSS files have been migrated from their original component directories to this centralized location. The original files have been deleted and all `styleUrls` references have been **removed** from component decorators.

### Migration Steps Completed:
1. ✅ Moved 15 component SCSS files to `components/`
2. ✅ Moved 8 page SCSS files to `pages/cleansia-partner/`
3. ✅ Created `components/index.scss` to import all component styles
4. ✅ Created `pages/cleansia-partner/index.scss` to import all page styles
5. ✅ Updated `cleansia-partner.scss` to import both index files
6. ✅ Removed all `styleUrls` from 36+ component decorators
7. ✅ Deleted original SCSS files from component directories

### Components Migrated (15 total)
- cleansia-availability
- cleansia-brand-name
- cleansia-button
- cleansia-dynamic-background
- cleansia-file
- cleansia-language-switcher
- cleansia-loader
- cleansia-registration-lock
- cleansia-section
- cleansia-select
- cleansia-sidebar-menu
- cleansia-table
- cleansia-telephone
- cleansia-title

### Pages Migrated (8 total)
- confirm-email
- invoice-detail
- invoices
- login
- order-details
- orders
- profile
- register
