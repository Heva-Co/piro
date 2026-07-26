package co.heva.piro.android.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.model.UserProfile
import co.heva.piro.shared.model.generated.UserNotificationPreferenceDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class ProfileUiState(
    val loading: Boolean = true,
    val profile: UserProfile? = null,
    val preferences: List<UserNotificationPreferenceDto> = emptyList(),
    val error: String? = null,
)

/** Loads the signed-in user's profile and notification preferences for the Profile tab. */
class ProfileViewModel(private val api: PiroApiClient) : ViewModel() {

    private val _state = MutableStateFlow(ProfileUiState())
    val state: StateFlow<ProfileUiState> = _state.asStateFlow()

    init {
        load()
    }

    private fun load() {
        viewModelScope.launch {
            _state.update { it.copy(loading = true, error = null) }
            try {
                val profile = api.me()
                // Preferences are best-effort — a failure there shouldn't blank the whole profile.
                val prefs = runCatching { api.getNotificationPreferences(profile.id) }.getOrDefault(emptyList())
                _state.update { it.copy(loading = false, profile = profile, preferences = prefs) }
            } catch (e: Exception) {
                _state.update { it.copy(loading = false, error = "Could not load your profile.") }
            }
        }
    }
}
