# Product

What Cleansia does, and the reasoning behind the numbers.

This section is written for the reader who needs to answer a customer, a cleaner or an auditor —
not for the one reading the code. It covers the feature surface and, more importantly, the
**business rules and why they are the values they are**.

## Why the "why" lives here

A number like *4 hours* or *24 hours free cancellation* is a decision, not a constant. Left in a C#
file it reads as arbitrary, so the next person changes it. Written down with its reasoning, it can be
argued with — which is the only way a business rule stays deliberate.

Rules documented here include the booking lead time and the express tier, the cancellation fee
ladder and its "oops window", how a crew size is derived from booked duration, what a Cleansia Plus
membership actually buys, and how cleaner pay is computed and bounded.

## Relationship to the rest of the site

| You want | Go to |
|---|---|
| What the rule *is*, and why that number | here |
| How it is enforced, step by step | [Flows](/flows/) |
| The nouns it operates on | [Domain](/domain/) |
| The argument that settled a contested one | [Decisions](/decisions/) |

## What is here now

The feature list and business-rule pages are being written. Until they land, the app sections
describe behaviour surface by surface.
