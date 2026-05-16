package cz.cleansia.customer.core.auth

import android.content.Context
import android.util.Log
import androidx.credentials.CredentialManager
import androidx.credentials.CustomCredential
import androidx.credentials.GetCredentialRequest
import androidx.credentials.exceptions.GetCredentialCancellationException
import androidx.credentials.exceptions.GetCredentialException
import androidx.credentials.exceptions.NoCredentialException
import com.google.android.libraries.identity.googleid.GetGoogleIdOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import com.google.android.libraries.identity.googleid.GoogleIdTokenParsingException
import cz.cleansia.customer.BuildConfig

/**
 * Thin wrapper around Credential Manager for the Sign in with Google flow.
 *
 * Returns a [GoogleSignInResult] so the caller (AuthViewModel) can branch on
 * outcome without catching credential-API exception types directly.
 *
 * The web client ID is the OAuth-2.0 "Web client" entry from Cloud Console,
 * NOT the Android client. Credential Manager exchanges it for an ID token
 * that the backend's GoogleAuth handler then validates.
 */
class GoogleSignInController(private val appContext: Context) {

    private val credentialManager = CredentialManager.create(appContext)

    /**
     * Launch the Google Account picker. Caller passes an Activity-scoped
     * context (NOT app context) so the bottom-sheet can attach to it.
     *
     * `filterByAuthorizedAccounts = false` keeps the picker showing every
     * Google account on the device — first-time users wouldn't otherwise
     * see an option to add an account.
     */
    suspend fun signIn(activityContext: Context): GoogleSignInResult {
        val webClientId = BuildConfig.GOOGLE_WEB_CLIENT_ID
        if (webClientId.isBlank()) {
            return GoogleSignInResult.NotConfigured
        }

        val option = GetGoogleIdOption.Builder()
            .setServerClientId(webClientId)
            .setFilterByAuthorizedAccounts(false)
            .build()

        val request = GetCredentialRequest.Builder()
            .addCredentialOption(option)
            .build()

        return try {
            val response = credentialManager.getCredential(activityContext, request)
            val credential = response.credential
            if (credential is CustomCredential
                && credential.type == GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
            ) {
                val googleCredential = GoogleIdTokenCredential.createFrom(credential.data)
                val first = googleCredential.givenName ?: ""
                val last = googleCredential.familyName ?: ""
                val email = googleCredential.id // googleid uses `id` as the email
                GoogleSignInResult.Success(
                    idToken = googleCredential.idToken,
                    googleId = googleCredential.id,
                    email = email,
                    firstName = first,
                    lastName = last,
                )
            } else {
                Log.w(TAG, "Unexpected credential type: ${credential.type}")
                GoogleSignInResult.Failure
            }
        } catch (e: GetCredentialCancellationException) {
            GoogleSignInResult.Cancelled
        } catch (e: NoCredentialException) {
            // No Google account on the device, or Play Services unavailable.
            Log.w(TAG, "No Google credential available: ${e.message}")
            GoogleSignInResult.NoAccount
        } catch (e: GetCredentialException) {
            Log.w(TAG, "Credential request failed: ${e.message}")
            GoogleSignInResult.Failure
        } catch (e: GoogleIdTokenParsingException) {
            Log.w(TAG, "Could not parse Google ID token: ${e.message}")
            GoogleSignInResult.Failure
        }
    }

    private companion object {
        const val TAG = "GoogleSignInController"
    }
}

sealed class GoogleSignInResult {
    data class Success(
        val idToken: String,
        val googleId: String,
        val email: String,
        val firstName: String,
        val lastName: String,
    ) : GoogleSignInResult()

    /** User dismissed the bottom sheet. UI should stay where it is, no error toast. */
    data object Cancelled : GoogleSignInResult()

    /** No Google account on device, or no Play Services. Tell the user to add one. */
    data object NoAccount : GoogleSignInResult()

    /** Build was shipped without GOOGLE_WEB_CLIENT_ID. Surface a clear dev error. */
    data object NotConfigured : GoogleSignInResult()

    /** Any other failure — token parsing, credential API, etc. */
    data object Failure : GoogleSignInResult()
}
