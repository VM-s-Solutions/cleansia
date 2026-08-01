#!/bin/bash
# Fail loudly: without this the script always exits 0 — even if sed fails or the client is
# missing — so `generate-*-client`'s && chain could not see a broken rename (T-0439).
set -euo pipefail
# Directory containing your TypeScript files
file="libs/core/customer-services/src/lib/client/customer-client.ts"
[ -f "$file" ] || { echo "formatter: $file not found — did the generator run?" >&2; exit 1; }
echo "Processing $file..."
# Use sed to rename classes and interfaces
sed -i.bak -E '
  s/(PagedData_1OfOf)([A-Za-z]+)(AndAppServicesAnd_0AndCulture_neutralAndPublicKeyToken_null)/\2PagedData/g; # Rename classes
  s/(I)(PagedData_1OfOf)([A-Za-z]+)(AndAppServicesAnd_0AndCulture_neutralAndPublicKeyToken_null)/I\3PagedData/g; # Rename interfaces
' "$file"
# Convert snake_case to camelCase for parameters starting with filter_ and sort_
sed -i.bak -E '
  s/filter_([a-zA-Z])/\L\1/g;
  s/sort_([a-z])/\L\1/g;
  s/_(.)/\U\1/g;
' "$file"
# Remove backup files created by sed
rm -f "${file}.bak"
echo "Renaming completed successfully."
# dos2unix customer-client-formatter.sh