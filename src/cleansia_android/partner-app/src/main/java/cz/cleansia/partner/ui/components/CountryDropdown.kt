package cz.cleansia.partner.ui.components

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MenuAnchorType
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldColors
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.Country

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CountryDropdown(
    label: String,
    selectedCountryId: String,
    countries: List<Country>,
    languageCode: String,
    onCountrySelected: (String) -> Unit,
    isError: Boolean = false,
    errorText: String? = null,
    colors: TextFieldColors = TextFieldDefaults.colors(
        focusedContainerColor = MaterialTheme.colorScheme.surfaceVariant,
        unfocusedContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.7f),
        focusedIndicatorColor = Color.Transparent,
        unfocusedIndicatorColor = Color.Transparent
    )
) {
    var expanded by remember { mutableStateOf(false) }
    var searchQuery by remember { mutableStateOf("") }
    val selectedName = countries.find { it.id == selectedCountryId }?.getLocalizedName(languageCode) ?: ""

    val filteredCountries = remember(searchQuery, countries, languageCode) {
        if (searchQuery.isBlank()) countries
        else countries.filter { it.getLocalizedName(languageCode).contains(searchQuery, ignoreCase = true) }
    }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it },
        modifier = Modifier.fillMaxWidth()
    ) {
        TextField(
            value = if (expanded) searchQuery else selectedName,
            onValueChange = { searchQuery = it },
            label = { Text(label) },
            modifier = Modifier
                .fillMaxWidth()
                .menuAnchor(MenuAnchorType.PrimaryEditable),
            singleLine = true,
            readOnly = false,
            shape = RoundedCornerShape(12.dp),
            colors = colors,
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            isError = isError,
            supportingText = errorText?.let { { Text(it) } }
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = {
                expanded = false
                searchQuery = ""
            }
        ) {
            filteredCountries.forEach { country ->
                DropdownMenuItem(
                    text = { Text(country.getLocalizedName(languageCode)) },
                    onClick = {
                        onCountrySelected(country.id)
                        expanded = false
                        searchQuery = ""
                    }
                )
            }
            if (filteredCountries.isEmpty()) {
                DropdownMenuItem(
                    text = {
                        Text(
                            stringResource(R.string.no_data),
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    },
                    onClick = {}
                )
            }
        }
    }
}
