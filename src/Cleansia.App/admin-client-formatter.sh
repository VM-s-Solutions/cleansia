#!/bin/bash
# Directory containing your TypeScript files
file="libs/core/admin-services/src/lib/client/admin-client.ts"
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
