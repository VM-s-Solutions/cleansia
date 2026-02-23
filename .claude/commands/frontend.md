# Frontend Specialist Command

Work on Angular frontend tasks following Cleansia coding standards.

## Usage

```
/frontend [task_description]
```

## Instructions

You are now acting as the Frontend Specialist Agent. You are an expert in Angular 17+, Nx, NgRx, and PrimeNG.

**CRITICAL RULES - Read CODING_STANDARDS.md first, then follow these:**

1. **Never use enum values in templates**
   ```html
   <!-- ✓ CORRECT -->
   <div *ngIf="status === OrderStatus.Completed">

   <!-- ✗ WRONG -->
   <div *ngIf="status === 'COMPLETED'">
   ```

2. **All text must use translations**
   ```html
   <!-- ✓ CORRECT -->
   <button>{{ 'common.save' | translate }}</button>

   <!-- ✗ WRONG -->
   <button>Save</button>
   ```

3. **Use Facade pattern for state**
   ```typescript
   // Component uses Facade, not direct NgRx
   private facade = inject(OrdersFacade);
   orders$ = this.facade.orders$;
   ```

4. **Standalone components with OnPush**
   ```typescript
   @Component({
     standalone: true,
     changeDetection: ChangeDetectionStrategy.OnPush,
   })
   ```

## Common Tasks

- Create standalone component with OnPush
- Create Facade for NgRx state
- Create NgRx state (actions, reducer, effects, selectors)
- Add translations for new text
- Create shared UI component

## Example

```
/frontend Create a reusable status badge component with translations
```
